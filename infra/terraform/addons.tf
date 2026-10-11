# Cluster add-ons installed with Helm (EKS-managed add-ons are in eks.tf).

resource "helm_release" "aws_lbc" {
  name       = "aws-load-balancer-controller"
  namespace  = "kube-system"
  repository = "https://aws.github.io/eks-charts"
  chart      = "aws-load-balancer-controller"
  version    = "3.6.0"

  set = [
    { name = "clusterName", value = module.eks.cluster_name },
    { name = "region", value = var.region },
    { name = "vpcId", value = module.vpc.vpc_id },
    { name = "serviceAccount.name", value = "aws-load-balancer-controller" },
  ]

  depends_on = [module.lbc_pod_identity, module.eks]
}

resource "helm_release" "external_secrets" {
  name             = "external-secrets"
  namespace        = "external-secrets"
  create_namespace = true
  repository       = "https://charts.external-secrets.io"
  chart            = "external-secrets"
  version          = "2.12.0"

  set = [
    { name = "installCRDs", value = "true" },
    { name = "serviceAccount.name", value = "external-secrets" },
  ]

  depends_on = [module.eso_pod_identity, module.eks]
}

# ClusterSecretStore "aws-secrets-manager" (referenced by values-eks.yaml). Installed via a raw chart so
# the plan doesn't need the ESO CRDs to exist yet (kubernetes_manifest would).
resource "helm_release" "cluster_secret_store" {
  name       = "aws-secrets-manager-store"
  namespace  = "external-secrets"
  repository = "https://bedag.github.io/helm-charts"
  chart      = "raw"
  version    = "2.0.2"

  values = [yamlencode({
    resources = [{
      apiVersion = "external-secrets.io/v1"
      kind       = "ClusterSecretStore"
      metadata   = { name = "aws-secrets-manager" }
      spec = {
        provider   = { aws = { service = "SecretsManager", region = var.region } } # auth: ESO pod identity
        conditions = [{ namespaces = [var.app_namespace] }]
      }
    }]
  })]

  depends_on = [helm_release.external_secrets]
}

resource "kubernetes_namespace_v1" "app" {
  metadata {
    name   = var.app_namespace
    labels = { "pod-security.kubernetes.io/enforce" = "restricted" } # chart is restricted-compliant
  }
  depends_on = [module.eks]
}
