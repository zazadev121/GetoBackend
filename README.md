# GETO Project — Backend
**Repo:** `zazadev121/GetoBackend`
**Deployed on:** Render

## ⚠️ CRITICAL — READ BEFORE PUSHING

> **DO NOT push the entire monorepo (`FullStackapp/`) here.**
>
> This repo contains **ONLY** the `apiprojnew/` folder (.NET API).
> The frontend (`Frontend/`) has its own separate repo → **[GetoFrontend](https://github.com/zazadev121/GetoFrontend)**
>
> Pushing the whole app to this repo almost broke the entire project.
> **Always push only backend changes here using git subtree.**

---

## How to push correctly

The monorepo lives at `d:\Csharp things brada\FullStackapp\`.
It has two subfolders — each goes to its own GitHub repo:

| Folder | GitHub Repo | Platform |
|---|---|---|
| `Frontend/` | **GetoFrontend** | Vercel |
| `apiprojnew/` | **GetoBackend** (this repo) | Render |

### Push backend changes (from monorepo root)
```bash
# From FullStackapp/ root
git add apiprojnew/
git commit -m "your message"
git push origin main   # syncs to GetoFrontend (monorepo)

# Then push ONLY apiprojnew/ to GetoBackend (Render reads from here)
$splitHash = git subtree split --prefix=apiprojnew HEAD
git push backend "${splitHash}:refs/heads/main" --force
```

> If `backend` remote is not set up yet:
> ```bash
> git remote add backend https://github.com/zazadev121/GetoBackend.git
> ```

---

## Tech Stack

- **Framework:** ASP.NET Core (.NET 8)
- **Language:** C#
- **Database:** PostgreSQL (via Neon)
- **ORM:** Entity Framework Core
- **Auth:** JWT Bearer tokens
- **Email:** EmailJS (verification codes only — no phase/status emails)
- **Push notifications:** Web Push (VAPID)
- **Container:** Docker (Dockerfile included)

## Install & run locally

```bash
cd apiprojnew
dotnet restore
dotnet run
```

## Key architecture notes

- **Emails** are sent ONLY for verification codes and password reset (`SmtpService.SendEmailAsync`)
- **Phase/status changes** use push notifications only — no emails
- **`SmtpService.SendNotificationEmailAsync` has been removed** — do not re-add it
- All admin notifications go through `WebPushService`

## Environment variables (set in Render dashboard)

| Variable | Purpose |
|---|---|
| `ConnectionStrings__Default` | PostgreSQL connection string |
| `Jwt__Key` | JWT signing key |
| `Jwt__Issuer` | JWT issuer |
| `Jwt__Audience` | JWT audience |
| `EmailJs__ServiceId` | EmailJS service ID |
| `EmailJs__TemplateId` | EmailJS template ID |
| `EmailJs__PublicKey` | EmailJS public key |
| `VapidPublicKey` | Web Push VAPID public key |
| `VapidPrivateKey` | Web Push VAPID private key |
