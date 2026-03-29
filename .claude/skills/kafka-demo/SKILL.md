---
name: kafka-demo
description: Start the full kafka-sensor-city demo from scratch. Starts minikube, deploys Kafka, creates topics, builds and deploys all services, and opens the dashboard.
allowed-tools: Bash(minikube *), Bash(kubectl *), Bash(docker *)
context: fork
---

You are setting up the kafka-sensor-city demo end to end. Work through the steps below in order. After each shell command, check the output before continuing — stop and report clearly if anything fails.

## Step 1 — minikube

Run:
```
minikube status
```

If it is not running, start it:
```
minikube start
```

Wait until `kubectl get nodes` shows one node with `STATUS = Ready`.

## Step 2 — Deploy Kafka

Apply the manifest:
```
kubectl apply -f k8s/kafka/kafka.yaml
```

Wait for `kafka-0` to reach `1/1 Running`. Poll with:
```
kubectl get pods -n kafka
```

Retry every 5 seconds, up to 2 minutes. If it is still not Running after 2 minutes, run `kubectl describe pod kafka-0 -n kafka` and report what you find.

## Step 3 — Create Kafka topics

Do NOT use an interactive exec. Run each command directly against the pod:

```
kubectl exec kafka-0 -n kafka -- /opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server kafka:9092 \
  --create --topic raw-sensor-events \
  --partitions 6 --replication-factor 1

kubectl exec kafka-0 -n kafka -- /opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server kafka:9092 \
  --create --topic intersection-events \
  --partitions 4 --replication-factor 1

kubectl exec kafka-0 -n kafka -- /opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server kafka:9092 \
  --create --topic signal-state-changes \
  --partitions 4 --replication-factor 1
```

Verify all three exist:
```
kubectl exec kafka-0 -n kafka -- /opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server kafka:9092 --list
```

If a topic already exists that is fine — continue.

## Step 4 — Point Docker at minikube

All four docker build commands below must prefix with `eval $(minikube docker-env) &&` so images land inside minikube, not your local Docker.

## Step 5 — Build and deploy all services

Run each block in sequence. Wait for the build to succeed before moving to the next.

```
eval $(minikube docker-env) && docker build -t sensor-simulator:latest ./SensorSimulator
kubectl apply -f k8s/sensor-simulator.yaml
```

```
eval $(minikube docker-env) && docker build -t filter-service:latest ./FilterService
kubectl apply -f k8s/filter-service.yaml
```

```
eval $(minikube docker-env) && docker build -t aggregator-service:latest ./AggregatorService
kubectl apply -f k8s/aggregator-service.yaml
```

```
eval $(minikube docker-env) && docker build -t dashboard-service:latest ./DashboardService
kubectl apply -f k8s/dashboard-service.yaml
```

## Step 6 — Wait for all pods

Poll until all five pods in the `kafka` namespace show `1/1 Running`:
```
kubectl get pods -n kafka
```

Expected pods: `kafka-0`, `sensor-simulator-*`, `filter-service-*`, `aggregator-service-*`, `dashboard-service-*`.

If any pod is in `CrashLoopBackOff` or `Error`, fetch its logs:
```
kubectl logs -n kafka <pod-name> --tail=40
```

Report what went wrong before stopping.

## Step 7 — Open the dashboard

```
minikube service dashboard-service -n kafka
```

Inform the user that their browser should open automatically and that **this terminal must stay open** — minikube holds the tunnel in the foreground on macOS. The city map will populate within a few seconds as the first intersection events arrive.
