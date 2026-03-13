# Security Audit Report: Directum MCP Server

**Date**: 2026-03-13 | **Auditor**: Claude Opus 4.6

## Summary: 0 CRITICAL, 4 HIGH, 4 MEDIUM, 3 LOW

| # | Finding | Severity | OWASP | Status |
|---|---------|----------|-------|--------|
| 1 | OData injection via unvalidated date/enum inputs | HIGH | A03 | TODO |
| 2 | Basic Auth over HTTP (not HTTPS) | HIGH | A02 | TODO |
| 3 | Hardcoded test credentials and service account name | MEDIUM | A07 | TODO |
| 4 | Path traversal in DevTools (arbitrary file read) | HIGH | A01 | TODO |
| 5 | Zip Slip in .dat package extraction | HIGH | A01 | TODO |
| 6 | XXE — safe by default, no explicit DTD prohibition | LOW | A05 | TODO |
| 7 | Error messages may leak server internals | MEDIUM | A04 | TODO |
| 8 | No explicit TLS certificate validation config | MEDIUM | A02 | TODO |
| 9 | No validation on entitySet / GetRawAsync URL suffix | MEDIUM | A03 | TODO |
| 10 | Password stored as plaintext string in memory | LOW | A02 | WONTFIX |
| 11 | No rate limiting on write operations | LOW | A04 | TODO |
| 12 | Dependencies — no known critical CVEs | INFO | — | OK |
