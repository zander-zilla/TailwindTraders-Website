# Optional TLS: ACM certificate for var.domain_name, DNS-validated in var.route53_zone_id.
# The ALB hostname is an output; point domain_name at it (CNAME/alias) after the first deploy.
locals {
  create_cert  = var.domain_name != ""
  validate_dns = local.create_cert && var.route53_zone_id != ""
}

resource "aws_acm_certificate" "shop" {
  count             = local.create_cert ? 1 : 0
  domain_name       = var.domain_name
  validation_method = "DNS"
  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_route53_record" "shop_validation" {
  for_each = local.validate_dns ? {
    for o in aws_acm_certificate.shop[0].domain_validation_options : o.domain_name => o
  } : {}
  zone_id         = var.route53_zone_id
  name            = each.value.resource_record_name
  type            = each.value.resource_record_type
  records         = [each.value.resource_record_value]
  ttl             = 300
  allow_overwrite = true
}

resource "aws_acm_certificate_validation" "shop" {
  count                   = local.validate_dns ? 1 : 0
  certificate_arn         = aws_acm_certificate.shop[0].arn
  validation_record_fqdns = [for r in aws_route53_record.shop_validation : r.fqdn]
}
