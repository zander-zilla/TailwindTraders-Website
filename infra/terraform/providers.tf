provider "aws" {
  region              = var.region
  allowed_account_ids = [var.account_id] # guard against applying to the wrong account

  default_tags {
    tags = merge({
      Project   = "tailwindtraders"
      Prefix    = var.name_prefix
      ManagedBy = "terraform"
      Repo      = var.github_repository
    }, var.tags)
  }
}

# Kubernetes/Helm talk to the new cluster with short-lived tokens (aws eks get-token).
provider "kubernetes" {
  host                   = module.eks.cluster_endpoint
  cluster_ca_certificate = base64decode(module.eks.cluster_certificate_authority_data)
  exec {
    api_version = "client.authentication.k8s.io/v1beta1"
    command     = "aws"
    args        = ["eks", "get-token", "--cluster-name", module.eks.cluster_name, "--region", var.region]
  }
}

provider "helm" {
  kubernetes = {
    host                   = module.eks.cluster_endpoint
    cluster_ca_certificate = base64decode(module.eks.cluster_certificate_authority_data)
    exec = {
      api_version = "client.authentication.k8s.io/v1beta1"
      command     = "aws"
      args        = ["eks", "get-token", "--cluster-name", module.eks.cluster_name, "--region", var.region]
    }
  }
}
