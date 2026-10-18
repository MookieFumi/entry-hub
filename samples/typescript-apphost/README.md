# TypeScript AppHost

The same application model as `src/EntryHub.AppHost/AppHost.cs`, written in TypeScript. The services stay in C#: the
AppHost only points at their project files.

```bash
cd samples/typescript-apphost
npm ci
aspire restore          # generates .aspire/modules, the typed SDK that apphost.mts imports
aspire run              # same resources and dashboard as the C# AppHost
```

The Aspire CLI is the one the C# AppHost already restores (`~/.nuget/packages/aspire.cli.linux-x64/<version>/tools/net10.0/linux-x64/aspire`)
or the [installed one](https://aspire.dev/get-started/install-cli/). Node 20.19, 22.13 or newer is required.

`npm run build` type-checks the AppHost and lints it.

## Integrations

Integrations are declared in `aspire.config.json` under `packages`, next to the SDK version:

```json
"packages": {
  "Aspire.Hosting.PostgreSQL": "13.6.1",
  "Aspire.Hosting.Kubernetes": "13.6.1-preview.1.26506.6"
}
```

`aspire add` does the same from the command line; `aspire restore` then regenerates `.aspire/modules`, which is not
committed.

## Publish

```bash
Parameters__jwt_issuer=x Parameters__jwt_audience=y Parameters__jwt_signing_key=z \
  aspire publish --non-interactive -o /tmp/chart
```

The chart is the one in `deploy/k8s`, with these differences:

- The parameters have no value of their own, so a non-interactive publish needs them as `Parameters__*` environment
  variables (any value: the chart keeps them as empty entries in `values.yaml`). The C# AppHost publishes without them.
- PostgreSQL has no PersistentVolumeClaim: the generated TypeScript SDK does not expose `WithPersistentVolume` in
  Aspire 13.6.1, so the data volume becomes an `emptyDir`. Use the C# AppHost for anything but a demo.
- The chart is named `aspire-managed` instead of `entryhub-apphost`.

## Dependencies

`package.json` is the one the Aspire template generates. `nodemon` (restarts the AppHost on changes during `aspire run`)
pulls in a version of `braces` that `npm audit` flags as high severity (a denial of service when it parses deeply nested
glob patterns). It is a development-only tool that never receives untrusted input here, so it stays until the template
moves to a patched release.
