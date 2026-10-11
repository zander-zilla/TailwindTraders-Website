# ---------- ECR ----------
resource "aws_ecr_repository" "web" {
  name                 = "tailwindtraders/web"
  image_tag_mutability = "IMMUTABLE" # tags = git SHAs
  force_delete         = !var.deletion_protection

  image_scanning_configuration {
    scan_on_push = true
  }

  encryption_configuration {
    encryption_type = "AES256"
  }
}

resource "aws_ecr_lifecycle_policy" "web" {
  repository = aws_ecr_repository.web.name
  policy = jsonencode({
    rules = [{
      rulePriority = 1
      description  = "keep the last 30 images"
      selection    = { tagStatus = "any", countType = "imageCountMoreThan", countNumber = 30 }
      action       = { type = "expire" }
    }]
  })
}

# ---------- DynamoDB cart (ADR 0008) ----------
resource "aws_dynamodb_table" "cart" {
  name                        = var.cart_table_name
  billing_mode                = "PAY_PER_REQUEST"
  hash_key                    = "userId"
  range_key                   = "productId"
  deletion_protection_enabled = var.deletion_protection

  attribute {
    name = "userId"
    type = "S"
  }

  attribute {
    name = "productId"
    type = "N"
  }

  ttl {
    attribute_name = "expiresAt"
    enabled        = true
  }

  point_in_time_recovery {
    enabled = true
  }

  server_side_encryption {
    enabled = true # AWS owned key -> AWS managed KMS key
  }
}

# ---------- Aurora PostgreSQL Serverless v2 (ADR 0007) ----------
module "aurora" {
  source  = "terraform-aws-modules/rds-aurora/aws"
  version = "~> 10.4"

  name            = "${local.name}-pg"
  engine          = "aurora-postgresql"
  engine_version  = var.aurora_engine_version
  engine_mode     = "provisioned"
  database_name   = var.database_name
  master_username = "tailwind_admin"

  manage_master_user_password = true # master secret in Secrets Manager (username/password JSON)

  cluster_instance_class = "db.serverless"
  instances              = { one = {} }
  serverlessv2_scaling_configuration = {
    min_capacity             = var.aurora_min_acu
    max_capacity             = var.aurora_max_acu
    seconds_until_auto_pause = var.aurora_min_acu == 0 ? 1800 : null
  }

  vpc_id               = module.vpc.vpc_id
  db_subnet_group_name = module.vpc.database_subnet_group_name
  security_group_ingress_rules = {
    eks_nodes = {
      referenced_security_group_id = module.eks.node_security_group_id
      description                  = "PostgreSQL from EKS nodes/pods"
    }
  }

  storage_encrypted   = true
  deletion_protection = var.deletion_protection
  skip_final_snapshot = !var.deletion_protection
  apply_immediately   = true
}

# ---------- JWT signing key (app SecurityKey) ----------
resource "random_password" "jwt" {
  length  = 64
  special = false
}

resource "aws_secretsmanager_secret" "jwt" {
  name                    = "${local.name}/jwt-signing-key"
  description             = "Tailwind Traders demo JWT HS256 signing key"
  recovery_window_in_days = var.deletion_protection ? 30 : 0
}

resource "aws_secretsmanager_secret_version" "jwt" {
  secret_id     = aws_secretsmanager_secret.jwt.id
  secret_string = random_password.jwt.result
}
