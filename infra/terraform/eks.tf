module "eks" {
  source  = "terraform-aws-modules/eks/aws"
  version = "~> 21.29"

  name               = "${local.name}-eks"
  kubernetes_version = var.kubernetes_version

  vpc_id     = module.vpc.vpc_id
  subnet_ids = module.vpc.private_subnets

  endpoint_public_access       = true
  endpoint_public_access_cidrs = var.cluster_endpoint_public_access_cidrs

  # Whoever runs `terraform apply` becomes cluster admin (bootstrap); more admins via admin_principal_arns.
  enable_cluster_creator_admin_permissions = true
  enable_irsa                              = false # EKS Pod Identity instead of IRSA

  addons = {
    coredns                = {}
    kube-proxy             = {}
    vpc-cni                = { before_compute = true }
    eks-pod-identity-agent = { before_compute = true }
    metrics-server         = {} # EKS community add-on (HPA)
    # CloudWatch agent + Fluent Bit: Container Insights, Application Signals, and an OTLP endpoint
    # (cloudwatch-agent.amazon-cloudwatch:4316) the app exports traces/metrics to -> X-Ray / CloudWatch.
    amazon-cloudwatch-observability = {
      pod_identity_association = [{
        role_arn        = module.cloudwatch_pod_identity.iam_role_arn
        service_account = "cloudwatch-agent"
      }]
    }
  }

  eks_managed_node_groups = {
    graviton = {
      ami_type       = "AL2023_ARM_64_STANDARD"
      instance_types = var.node_instance_types
      capacity_type  = "ON_DEMAND"
      min_size       = var.node_min_size
      max_size       = var.node_max_size
      desired_size   = var.node_desired_size
      labels         = { "kubernetes.io/arch-class" = "graviton" }
    }
  }

  access_entries = merge(
    {
      # GitHub Actions deploy role: admin only inside the app namespace.
      github_deploy = {
        principal_arn = aws_iam_role.github_deploy.arn
        policy_associations = {
          app_ns = {
            policy_arn   = "arn:aws:eks::aws:cluster-access-policy/AmazonEKSAdminPolicy"
            access_scope = { type = "namespace", namespaces = [var.app_namespace] }
          }
        }
      }
    },
    {
      for i, arn in var.admin_principal_arns : "admin_${i}" => {
        principal_arn = arn
        policy_associations = {
          admin = {
            policy_arn   = "arn:aws:eks::aws:cluster-access-policy/AmazonEKSClusterAdminPolicy"
            access_scope = { type = "cluster" }
          }
        }
      }
    }
  )
}
