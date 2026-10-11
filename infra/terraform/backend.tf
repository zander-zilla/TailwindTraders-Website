# Remote state (recommended before the first real apply). Create the bucket once, then uncomment:
#   aws s3api create-bucket --bucket <prefix>-tfstate-<account-id> --region <region> \
#     --create-bucket-configuration LocationConstraint=<region>       # omit for us-east-1
#   aws s3api put-bucket-versioning --bucket <prefix>-tfstate-<account-id> --versioning-configuration Status=Enabled
#   terraform init -migrate-state
#
# terraform {
#   backend "s3" {
#     bucket       = "<prefix>-tfstate-<account-id>"
#     key          = "tailwindtraders/terraform.tfstate"
#     region       = "<region>"
#     encrypt      = true
#     use_lockfile = true   # S3-native locking (Terraform >= 1.10); no DynamoDB lock table needed
#   }
# }
