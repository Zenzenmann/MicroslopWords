# Security Policy

## Supported Versions

MicroslopWords is pre-1.0. Security fixes are applied to the `main` branch and the latest release.

## Reporting a Vulnerability

Do not open a public issue for a security vulnerability.

Use GitHub's private vulnerability reporting:

1. Go to the repository's **Security** tab.
2. Select **Report a vulnerability**.
3. Include affected versions, reproduction steps, and impact.

If private reporting is unavailable, contact the maintainers listed in the repository profile. Do not include secrets, personal documents, or exploit code in public channels.

## Scope

High-value areas include:

- DOCX parser memory safety and zip-bomb handling
- Path traversal during import/export
- Unsafe deserialization
- Update and release signing
- Handling of untrusted document content

## Disclosure

We aim to acknowledge reports within 72 hours and to publish a coordinated fix and advisory. Please allow time for a release before public disclosure.
