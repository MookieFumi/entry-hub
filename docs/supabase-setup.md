# Setting up Supabase for the demo

The demo uses one Supabase project for two things: the PostgreSQL database and sign-in (Supabase Auth). This page has
everything to set it up, so you do not need anything outside the repository. What GitHub and Azure need is in
[deployment.md](deployment.md). Run the SQL in the *SQL Editor* of the project, as the `postgres` role.

## 1. Project

1. Create an organization (Free plan) and a project, for example `entryhub-demo`, in an EU region close to West
   Europe (Frankfurt or Ireland) to keep latency to Azure low.
2. Keep the database password (the one of the `postgres` owner role) somewhere safe.
3. Note the **project ref**, the 20 characters of `https://<ref>.supabase.co`.
4. *Database → Extensions*: enable `btree_gist`. The exclusion constraint that prevents double bookings needs it; the
   migration also creates it, but enabling it first avoids permission surprises.

## 2. Two database roles

Migrations run with the owner (`postgres`), the only role that can create tables and policies. The API runs with an
ordinary role that cannot change the schema. Supabase's `postgres` role has `BYPASSRLS`, so if the API used it,
Row-Level Security would stop isolating the tenants. Do this **before the first migration**, so the tables are born
with the right permissions. Change the password.

```sql
create role entryhub login password 'CHANGE-THIS-PASSWORD'
  nosuperuser nobypassrls nocreatedb nocreaterole;

grant connect on database postgres to entryhub;
grant usage on schema public to entryhub;

-- Tables that postgres creates (the migrations): the application only reads and writes rows.
alter default privileges for role postgres in schema public
  grant select, insert, update, delete on tables to entryhub;

-- Nothing for the Supabase Data API roles, now or on future tables.
alter default privileges for role postgres in schema public revoke all on tables    from anon, authenticated;
alter default privileges for role postgres in schema public revoke all on sequences from anon, authenticated;
alter default privileges for role postgres in schema public revoke all on functions from anon, authenticated;
```

## 3. Close the Data API

Supabase publishes the `public` schema through a REST API (PostgREST) with the `anon` key. Some tables (`tenants`,
the equipment catalog) have no Row-Level Security, so they must not be exposed. In *Project Settings → Data API*
turn the Data API off. This does not affect Supabase Auth, which is another service.

After the first migration, check that nothing is left for `anon` or `authenticated`:

```sql
select grantee, table_name, privilege_type
from information_schema.role_table_grants
where table_schema = 'public' and grantee in ('anon', 'authenticated');
-- must return 0 rows; if not: revoke all on all tables in schema public from anon, authenticated;
```

## 4. Connection strings

Use the **session pooler** (*Connect* in the project, port 5432). Azure App Service and GitHub runners reach Supabase
over IPv4, and the direct connection is IPv6 unless you buy the IPv4 add-on. Session mode, not transaction mode,
because Npgsql and EF Core need prepared statements and the migration lock. The user is `<role>.<project-ref>`.

```text
Host=<pooler-host>;Port=5432;Database=postgres;Username=<role>.<ref>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true
```

| Use | Where it is stored | User |
|---|---|---|
| Migrations | Secret `MIGRATIONS_CONNECTION_STRING` of the GitHub environment `production` | `postgres.<ref>` |
| The API | App setting `ConnectionStrings__EntryHub` of the App Service | `entryhub.<ref>` |

`Trust Server Certificate=true` is fine for a demo. For a real deployment install the Supabase root certificate and
use `SSL Mode=VerifyFull`.

## 5. Sign-in and the Custom Access Token Hook

The API validates Supabase tokens like those of any OpenID Connect provider (through the JWKS). The tenant and the
role of the caller come from two claims that a hook adds to every token.

1. *Project Settings → JWT Keys*: the project must sign with an asymmetric key. Its public keys are at
   `https://<ref>.supabase.co/auth/v1/.well-known/jwks.json`. With only the legacy HS256 secret the API cannot
   validate tokens through `Authority`; migrate the signing key from that panel.
2. *Authentication → Sign In / Providers*: untick "Allow new users to sign up". You create the users.
3. Create the table of memberships and the hook. Everything lives in a `private` schema that the Data API does not
   expose.

   ```sql
   create schema if not exists private;

   create table private.user_tenants (
     user_id   uuid primary key references auth.users (id) on delete cascade,
     tenant_id uuid not null,
     app_role  text not null check (app_role in ('Employee', 'Facilities'))
   );

   create or replace function private.custom_access_token_hook(event jsonb)
   returns jsonb
   language plpgsql
   stable
   as $$
   declare
     claims jsonb := event -> 'claims';
     membership record;
   begin
     select tenant_id, app_role into membership
     from private.user_tenants
     where user_id = (event ->> 'user_id')::uuid;

     if found then
       claims := jsonb_set(claims, '{tenant_id}', to_jsonb(membership.tenant_id::text));
       claims := jsonb_set(claims, '{user_role}', to_jsonb(membership.app_role));
     end if;

     return jsonb_set(event, '{claims}', claims);
   end;
   $$;

   grant usage on schema private to supabase_auth_admin;
   grant select on private.user_tenants to supabase_auth_admin;
   grant execute on function private.custom_access_token_hook to supabase_auth_admin;
   revoke execute on function private.custom_access_token_hook from authenticated, anon, public;
   ```

4. *Authentication → Hooks* → *Custom Access Token* → Postgres function → `private.custom_access_token_hook`.

**Why `user_role` and not `role`:** in Supabase `role` is `authenticated`, its own PostgreSQL role. The API reads the
application role from the claim named in `Authentication__RoleClaim`; with Supabase set it to `user_role`. The
`role` claim grants nothing.

The API settings that go with this (App Service app settings; `__` is the `:` of configuration):

| Setting | Value |
|---|---|
| `Authentication__Schemes__Bearer__Authority` | `https://<ref>.supabase.co/auth/v1` |
| `Authentication__Schemes__Bearer__ValidAudiences__0` | `authenticated` |
| `Authentication__RoleClaim` | `user_role` |

## 6. Demo users

The API creates two demo tenants when it starts with `Demo__Seed=true`: **Acme Corporation**
(`de000000-0000-0000-0000-0000000000a1`) and **Globex Industries** (`de000000-0000-0000-0000-0000000000a2`).
Create four users, two per tenant.

1. *Authentication → Users → Add user → Create new user*, with **Auto Confirm User** ticked (Supabase's default mail
   sender has a very low limit). Use a different password for each.

   | Email | Tenant | Role |
   |---|---|---|
   | `ana@acme.example` | Acme | Employee |
   | `fran@acme.example` | Acme | Facilities |
   | `gael@globex.example` | Globex | Employee |
   | `gina@globex.example` | Globex | Facilities |

   If Supabase rejects the `.example` domain, use one you control and change the emails in the SQL below.

2. Give them a tenant and a role (it looks users up by email, so no ids to copy):

   ```sql
   insert into private.user_tenants (user_id, tenant_id, app_role)
   select u.id, v.tenant_id::uuid, v.app_role
   from (values
     ('ana@acme.example',    'de000000-0000-0000-0000-0000000000a1', 'Employee'),
     ('fran@acme.example',   'de000000-0000-0000-0000-0000000000a1', 'Facilities'),
     ('gael@globex.example', 'de000000-0000-0000-0000-0000000000a2', 'Employee'),
     ('gina@globex.example', 'de000000-0000-0000-0000-0000000000a2', 'Facilities')
   ) as v (email, tenant_id, app_role)
   join auth.users u on u.email = v.email;
   -- must insert 4 rows
   ```

3. Check a token. The publishable (anon) key is under *Connect*; it is not a secret. Never use the `service_role` key.

   ```bash
   curl -s -X POST "https://<ref>.supabase.co/auth/v1/token?grant_type=password" \
     -H "apikey: <publishable-key>" -H "Content-Type: application/json" \
     -d '{"email":"fran@acme.example","password":"<password>"}'
   ```

   `access_token` is the token. To read its claims without sending it anywhere:
   `echo "<access_token>" | cut -d. -f2 | base64 -d` (add `=` at the end if it complains). You should see
   `iss = https://<ref>.supabase.co/auth/v1`, `aud = authenticated`, `tenant_id` and `user_role`.

Then `GET /api/v1/me` with that token must answer the tenant and role. That part can only be checked after the first
deployment, because the demo tenants exist once the API has started once with `Demo__Seed=true`.

Not covered by the demo: password recovery by email, social login, and federation with a customer's own provider.
