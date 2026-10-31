# Two instances: what breaks when an API stops running as one

Two small projects. Nothing in `EntryHub.Api` uses them.

| Project | Port | What it is |
|---|---|---|
| `ProductsApi` | 5501 and 5502 | `GET /products/{id}` reads through a cache; `PUT /products/{id}/price/{price}` changes the price. Both instances share a JSON file as their "database". The cache is `IMemoryCache` or Redis (`Cache:Mode`), and the built-in rate limiter allows 20 requests per minute per `X-Api-Key` (`RateLimit:Enabled`) |
| `Balancer` | 5500 | YARP in front of both instances, round robin. `Balancer:RoutesFile` picks `balancer.round-robin.json`, `balancer.affinity.json` (cookie session affinity) or `balancer.limit.json` (a limit of 20 per minute per API key in the balancer) |

## What `scripts/test-two-instances.sh` measures

1. **In-memory cache.** After the price changes from 10 to 20, 10 reads through the balancer: 5 answer 10 and 5 answer 20.
2. **Redis cache.** The same 10 reads all answer 20.
3. **Session affinity with the in-memory cache.** The client that changed the price reads 20 every time; another client pinned to the other instance reads 10 every time.
4. **Rate limit.** With the built-in limiter in each instance, 30 requests with one API key all get `200`: two instances double the limit. With the limit in the balancer, 20 get `200` and 10 get `429`.

```bash
scripts/test-two-instances.sh
```

Step 2 needs `redis-server` and is skipped without it. The script runs in the `gateway` job of the CI.
