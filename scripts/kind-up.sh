#!/usr/bin/env bash
# Bring up Tailwind Traders on a local kind cluster (macOS Apple Silicon or Linux).
# Needs: docker (Docker Desktop), kind, kubectl, helm   ->  brew install kind helm kubectl
# Usage: scripts/kind-up.sh            (create/reuse cluster, build+load image, install chart)
#        scripts/kind-up.sh --down     (delete the cluster)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CLUSTER="${CLUSTER:-tailwindtraders}"
NS="${NS:-tailwindtraders}"
IMAGE="${IMAGE:-tailwindtraders/web:local}"
CHART="$ROOT/deploy/helm/tailwindtraders"

if [[ "${1:-}" == "--down" ]]; then kind delete cluster --name "$CLUSTER"; exit 0; fi
for t in docker kind kubectl helm; do command -v "$t" >/dev/null || { echo "missing $t (brew install kind helm kubectl)"; exit 1; }; done

if ! kind get clusters | grep -qx "$CLUSTER"; then
  kind create cluster --name "$CLUSTER" --config "$ROOT/deploy/kind/kind-config.yaml" --wait 120s
fi
kubectl config use-context "kind-$CLUSTER" >/dev/null

echo "==> Building $IMAGE for the native architecture ($(uname -m))"
docker build -t "$IMAGE" "$ROOT/TailwindTraders.Website/Source/Tailwind.Traders.Web"
kind load docker-image "$IMAGE" --name "$CLUSTER"

echo "==> Local dependencies (postgres:17, dynamodb-local)"
kubectl create namespace "$NS" --dry-run=client -o yaml | kubectl apply -f -
kubectl -n "$NS" apply -f "$ROOT/deploy/kind/dependencies.yaml"
kubectl -n "$NS" rollout status deploy/postgres --timeout=180s
kubectl -n "$NS" rollout status deploy/dynamodb --timeout=180s

echo "==> Helm install (pre-install hook runs --migrate)"
# Restart pods on re-runs so a rebuilt :local image is picked up.
helm upgrade --install tailwindtraders "$CHART" -n "$NS" -f "$CHART/values-kind.yaml" \
  --set-string podAnnotations.rebuilt="$(date +%s)" --wait --timeout 5m

echo "==> Smoke test"
for i in $(seq 1 30); do curl -fsS http://localhost:2000/readyz >/dev/null 2>&1 && break; sleep 2; done
curl -fsS http://localhost:2000/healthz && echo " (liveness)"
curl -fsS http://localhost:2000/readyz && echo " (readiness: postgres + dynamodb)"
echo
echo "Open http://localhost:2000   |   kubectl -n $NS get pods   |   scripts/kind-up.sh --down"
