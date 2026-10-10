namespace Tailwind.Traders.Web.Standalone
{
    static class Constants
    {
        public const string DefaultJwtSigningKey = "Ta!lwindTraders-demo-signing-key-min-256-bits!!"; // .NET 10 IdentityModel requires >= 256-bit HS256 keys (legacy key was 176 bits). Demo only — set SecurityKey via secrets.;
    }
}