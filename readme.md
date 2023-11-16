# Test repo for opentelemetry collector 


## Running prometheus locally
```bash
docker run \
    -p 9090:9090 \
    -v ./prometheus/prometheus.yml:/prometheus/prometheus.yml \
    --name prom --rm --network host \
    prom/prometheus --enable-feature=otlp-write-receiver --web.enable-remote-write-receiver
```

### Run some node exporters
```bash
./node_exporter/node_exporter --web.listen-address 127.0.0.1:8080 &
./node_exporter/node_exporter --web.listen-address 127.0.0.1:8081 &
./node_exporter/node_exporter --web.listen-address 127.0.0.1:8082

```

### Grafana
`docker run -d --name=grafana -p 3000:3000 grafana/grafana-enterprise`

### Otel Collector
`docker run -v ./config.yaml:/etc/otelcol/config.yaml -p 127.0.0.1:4317:4317 -p 127.0.0.1:55679:55679 --name otel --rm -d otel/opentelemetry-collector:0.88.0`
