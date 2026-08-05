# Security Policy

## Supported Versions

This is a starter kit template, not a versioned library — there are no
maintained release branches. Security fixes are made against the latest
commit on `main`; if you've cloned/forked this kit into your own project, you
are responsible for pulling in fixes yourself (or applying them manually once
you've diverged).

## Reporting a Vulnerability

**Please do not open a public GitHub issue for security vulnerabilities.**

Preferred: use GitHub's private vulnerability reporting for this repository —
[Report a vulnerability](https://github.com/DaoNguyenTrong/DAONT-BASE/security/advisories/new).

If that's not available to you, email **dao12a2@gmail.com** with:

- A description of the vulnerability and its impact
- Steps to reproduce (a minimal repro is very helpful)
- Any relevant logs, requests/responses, or PoC code

You should expect an initial response within a few days. This is a
single-maintainer open-source project, so timelines are best-effort — please
be patient, and follow up if you haven't heard back after a week.

Please give a reasonable amount of time to investigate and release a fix
before any public disclosure.

## Scope

In scope: vulnerabilities in the code under `backend/`, `frontend/`, and
`shared/` in this repository, as shipped.

Out of scope:

- Vulnerabilities in third-party dependencies — please report those upstream
  (though a heads-up here is still welcome so this project can update the
  dependency).
- Issues that only apply after you've modified the starter kit's security
  posture (e.g. removed auth middleware, changed CORS/cookie settings,
  disabled rate limiting) as part of building your own application on top
  of it.
- Deployment/infrastructure misconfiguration in your own environment (secrets
  management, TLS termination, reverse proxy setup, etc.) — see the
  `.claude/rules/` docs for the assumptions this project makes about its
  deployment environment.
