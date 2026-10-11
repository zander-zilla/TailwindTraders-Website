# ---------- EKS Pod Identity (no IRSA) ----------
module "app_pod_identity" {
  source  = "terraform-aws-modules/eks-pod-identity/aws"
  version = "~> 2.9"

  name = "${local.name}-app"

  attach_custom_policy = true
  policy_statements = [
    {
      sid       = "CartTable"
      actions   = ["dynamodb:Query", "dynamodb:GetItem", "dynamodb:UpdateItem", "dynamodb:DeleteItem", "dynamodb:DescribeTable"]
      resources = [aws_dynamodb_table.cart.arn]
    },
    {
      sid       = "ReadAppSecrets"
      actions   = ["secretsmanager:GetSecretValue", "secretsmanager:DescribeSecret"]
      resources = [aws_secretsmanager_secret.jwt.arn, module.aurora.cluster_master_user_secret[0].secret_arn]
    },
    {
      # Phase 8: embeddings (search + migrate Job) and the optional assistant. Only these two models.
      # Cross-region inference profiles ("us.<model>") route to the model in several regions, hence region "*".
      sid     = "BedrockInvoke"
      actions = ["bedrock:InvokeModel"]
      resources = [
        "arn:aws:bedrock:*::foundation-model/${var.bedrock_embedding_model_id}",
        "arn:aws:bedrock:*::foundation-model/${trimprefix(trimprefix(var.bedrock_chat_model_id, "us."), "eu.")}",
        "arn:aws:bedrock:${var.region}:${var.account_id}:inference-profile/${var.bedrock_chat_model_id}",
      ]
    },
  ]

  associations = {
    web = {
      cluster_name    = module.eks.cluster_name
      namespace       = var.app_namespace
      service_account = var.app_service_account
    }
  }
}

module "eso_pod_identity" {
  source  = "terraform-aws-modules/eks-pod-identity/aws"
  version = "~> 2.9"

  name = "${local.name}-external-secrets"

  attach_external_secrets_policy        = true
  external_secrets_secrets_manager_arns = [aws_secretsmanager_secret.jwt.arn, module.aurora.cluster_master_user_secret[0].secret_arn]
  external_secrets_create_permission    = false

  associations = {
    eso = {
      cluster_name    = module.eks.cluster_name
      namespace       = "external-secrets"
      service_account = "external-secrets"
    }
  }
}

module "cloudwatch_pod_identity" {
  source  = "terraform-aws-modules/eks-pod-identity/aws"
  version = "~> 2.9"

  name = "${local.name}-cloudwatch-agent"

  attach_aws_cloudwatch_observability_policy = true                                                        # CloudWatchAgentServerPolicy
  additional_policy_arns                     = { xray = "arn:aws:iam::aws:policy/AWSXrayWriteOnlyAccess" } # traces -> X-Ray
}

module "lbc_pod_identity" {
  source  = "terraform-aws-modules/eks-pod-identity/aws"
  version = "~> 2.9"

  name = "${local.name}-aws-lbc"

  attach_aws_lb_controller_policy = true

  associations = {
    lbc = {
      cluster_name    = module.eks.cluster_name
      namespace       = "kube-system"
      service_account = "aws-load-balancer-controller"
    }
  }
}

# ---------- GitHub Actions OIDC (ADR 0010) ----------
resource "aws_iam_openid_connect_provider" "github" {
  count          = var.create_github_oidc_provider ? 1 : 0
  url            = "https://token.actions.githubusercontent.com"
  client_id_list = ["sts.amazonaws.com"]
}

data "aws_iam_openid_connect_provider" "github" {
  count = var.create_github_oidc_provider ? 0 : 1
  url   = "https://token.actions.githubusercontent.com"
}

locals {
  github_oidc_arn = var.create_github_oidc_provider ? aws_iam_openid_connect_provider.github[0].arn : data.aws_iam_openid_connect_provider.github[0].arn
}

data "aws_iam_policy_document" "github_trust" {
  statement {
    actions = ["sts:AssumeRoleWithWebIdentity"]
    principals {
      type        = "Federated"
      identifiers = [local.github_oidc_arn]
    }
    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }
    # Only the main branch or the protected "aws" environment of this one repository.
    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:sub"
      values = [
        "repo:${var.github_repository}:ref:refs/heads/main",
        "repo:${var.github_repository}:environment:${var.github_environment}",
      ]
    }
  }
}

resource "aws_iam_role" "github_deploy" {
  name                 = "${local.name}-github-deploy"
  description          = "GitHub Actions (OIDC) deploy role for ${var.github_repository}"
  assume_role_policy   = data.aws_iam_policy_document.github_trust.json
  max_session_duration = 3600
}

data "aws_iam_policy_document" "github_deploy" {
  statement {
    sid       = "EcrAuth"
    actions   = ["ecr:GetAuthorizationToken"]
    resources = ["*"]
  }
  statement {
    sid = "EcrPush"
    actions = [
      "ecr:BatchCheckLayerAvailability", "ecr:BatchGetImage", "ecr:CompleteLayerUpload", "ecr:DescribeImages",
      "ecr:GetDownloadUrlForLayer", "ecr:InitiateLayerUpload", "ecr:PutImage", "ecr:UploadLayerPart",
    ]
    resources = [aws_ecr_repository.web.arn]
  }
  statement {
    sid       = "EksDescribe" # for `aws eks update-kubeconfig`; Kubernetes RBAC comes from the EKS access entry
    actions   = ["eks:DescribeCluster"]
    resources = [module.eks.cluster_arn]
  }
}

resource "aws_iam_role_policy" "github_deploy" {
  name   = "deploy"
  role   = aws_iam_role.github_deploy.id
  policy = data.aws_iam_policy_document.github_deploy.json
}
