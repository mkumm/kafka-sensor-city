---
name: kafka-demo
description: Manage the kafka-sensor-city demo. Run with no arguments to start the full demo. Run with "teardown" to remove all resources. Run with "teardown full" to also delete the minikube cluster.
allowed-tools: Bash(minikube *), Bash(kubectl *), Bash(docker *)
context: fork
---

Check `$ARGUMENTS` first and branch accordingly:

- If `$ARGUMENTS` starts with `teardown` → jump to the **TEARDOWN** section below.
- Otherwise → run the **SETUP** section.

---

# TEARDOWN

Determine the teardown scope from `$ARGUMENTS`:
- `teardown` alone → remove all demo resources but leave minikube running
- `teardown full` → remove all demo resources **and** delete the minikube cluster

## Step T1 — Delete all demo deployments

```
kubectl delete deployment sensor-simulator aggregator-service filter-service dashboard-service -n kafka --ignore-not-found
```

Confirm they are gone:
```
kubectl get deployments -n kafka
```

## Step T2 — Delete the kafka namespace

This removes Kafka, all services, PVCs, and the ConfigMap in one shot:
```
kubectl delete namespace kafka --ignore-not-found
```

Wait until the namespace is fully terminated:
```
kubectl get namespace kafka
```

It should return `Error from server (NotFound)` — retry up to 60 seconds.

## Step T3 — minikube (conditional)

If `$ARGUMENTS` is `teardown full`:
```
minikube delete
```
Inform the user the entire cluster and all cached images have been removed.

If `$ARGUMENTS` is just `teardown`:
Inform the user that minikube is still running. They can run `/kafka-demo` to redeploy, or `minikube stop` / `minikube delete` manually when done.

---

# SETUP

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
