output "region" {
  value = var.region
}

output "cluster_name" {
  value = module.eks.cluster_name
}

output "ecr_repository_url" {
  value = aws_ecr_repository.web.repository_url
}

output "aurora_endpoint" {
  value = module.aurora.cluster_endpoint
}

output "aurora_port" {
  value = module.aurora.cluster_port
}

output "aurora_master_secret_arn" {
  value = module.aurora.cluster_master_user_secret[0].secret_arn
}

output "jwt_secret_name" {
  value = aws_secretsmanager_secret.jwt.name
}

output "cart_table_name" {
  value = aws_dynamodb_table.cart.name
}

output "github_deploy_role_arn" {
  value = aws_iam_role.github_deploy.arn
}

output "acm_certificate_arn" {
  value = local.create_cert ? aws_acm_certificate.shop[0].arn : ""
}

output "kubeconfig_command" {
  value = "aws eks update-kubeconfig --name ${module.eks.cluster_name} --region ${var.region}"
}

# Repository *variables* (not secrets) consumed by .github/workflows/deploy-aws.yaml. Set them with:
#   terraform output -json github_variables | jq -r 'to_entries[] | "\(.key) \(.value)"' |
#     while read k v; do gh variable set "$k" --body "$v" --repo zander-zilla/TailwindTraders-Website; done
output "github_variables" {
  value = {
    AWS_ROLE_ARN        = aws_iam_role.github_deploy.arn
    AWS_REGION          = var.region
    EKS_CLUSTER_NAME    = module.eks.cluster_name
    ECR_REPOSITORY_URL  = aws_ecr_repository.web.repository_url
    AURORA_ENDPOINT     = module.aurora.cluster_endpoint
    AURORA_SECRET_ARN   = module.aurora.cluster_master_user_secret[0].secret_arn
    JWT_SECRET_NAME     = aws_secretsmanager_secret.jwt.name
    CART_TABLE_NAME     = aws_dynamodb_table.cart.name
    APP_HOST            = var.domain_name
    ACM_CERTIFICATE_ARN = local.create_cert ? aws_acm_certificate.shop[0].arn : ""
  }
}

# Same mapping as a Helm values overlay for manual deploys:
#   terraform output -raw helm_values > ../../deploy/helm/tailwindtraders/values-eks.generated.yaml
output "helm_values" {
  value = yamlencode({
    image = { repository = aws_ecr_repository.web.repository_url }
    config = {
      Cart__TableName   = aws_dynamodb_table.cart.name
      Cart__Region      = var.region
      Cart__CreateTable = "false"
    }
    secrets = {
      externalSecret = {
        dbSecretKey  = module.aurora.cluster_master_user_secret[0].secret_arn
        dbHost       = module.aurora.cluster_endpoint
        dbPort       = tostring(module.aurora.cluster_port)
        database     = var.database_name
        jwtSecretKey = aws_secretsmanager_secret.jwt.name
      }
    }
    ingress = merge(
      { host = var.domain_name },
      local.create_cert ? { annotations = {
        "alb.ingress.kubernetes.io/scheme"           = "internet-facing"
        "alb.ingress.kubernetes.io/target-type"      = "ip"
        "alb.ingress.kubernetes.io/healthcheck-path" = "/readyz"
        "alb.ingress.kubernetes.io/group.name"       = "tailwindtraders"
        "alb.ingress.kubernetes.io/listen-ports"     = jsonencode([{ HTTP = 80 }, { HTTPS = 443 }])
        "alb.ingress.kubernetes.io/ssl-redirect"     = "443"
        "alb.ingress.kubernetes.io/certificate-arn"  = aws_acm_certificate.shop[0].arn
      } } : {}
    )
  })
}
