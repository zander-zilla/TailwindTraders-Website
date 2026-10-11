data "aws_availability_zones" "available" {
  state = "available"
  filter {
    name   = "opt-in-status"
    values = ["opt-in-not-required"]
  }
}

locals {
  name = var.name_prefix
  azs  = slice(data.aws_availability_zones.available.names, 0, 3)
}

module "vpc" {
  source  = "terraform-aws-modules/vpc/aws"
  version = "~> 6.7"

  name = "${local.name}-vpc"
  cidr = var.vpc_cidr
  azs  = local.azs

  private_subnets  = [for i, _ in local.azs : cidrsubnet(var.vpc_cidr, 4, i)]      # /20 per AZ for pods (VPC CNI)
  public_subnets   = [for i, _ in local.azs : cidrsubnet(var.vpc_cidr, 8, 48 + i)] # ALB
  database_subnets = [for i, _ in local.azs : cidrsubnet(var.vpc_cidr, 8, 52 + i)] # Aurora

  enable_nat_gateway     = true
  single_nat_gateway     = true # demo cost: one NAT (not AZ-resilient)
  one_nat_gateway_per_az = false

  create_database_subnet_group = true

  public_subnet_tags  = { "kubernetes.io/role/elb" = 1 }
  private_subnet_tags = { "kubernetes.io/role/internal-elb" = 1 }
}
