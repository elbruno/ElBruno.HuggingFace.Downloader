# Routing Rules

Route work to squad members based on task domain and agent expertise.

## By Domain

| Domain | Primary | Secondary | Reviewers |
|--------|---------|-----------|-----------|
| **Architecture / Design** | Neo | Trinity | Neo |
| **Core API & Implementation** | Trinity | Neo | Neo |
| **Service Integration / DI** | Trinity | Neo | Neo |
| **Test Strategy & Coverage** | Agent Smith | Trinity | Neo |
| **Documentation / Examples** | Morpheus | Trinity | Neo |
| **Publishing / Releases** | Morpheus | Neo | Neo |
| **Performance & Caching** | Trinity | Agent Smith | Neo |
| **Error Handling & Resilience** | Trinity | Agent Smith | Neo |
| **NuGet Package** | Morpheus | Trinity | Neo |

## By Task Type

| Signal | Route To | Mode |
|--------|----------|------|
| "Fix bug in download logic" | Trinity | Standard |
| "Add error handling for X" | Trinity → Agent Smith (reviewer) | Standard |
| "Design the API for X" | Neo → Trinity (implementation) | Standard |
| "Write tests for X" | Agent Smith | Standard |
| "Update documentation for X" | Morpheus | Lightweight |
| "Release to NuGet" | Morpheus → Neo (approval) | Sync |
| "Team, build feature X" | Neo (lead) + Trinity + Agent Smith + Morpheus | Full |
| "What's our architecture for X?" | Neo | Direct |

## Reviewer Gates

- **Code review gate:** Neo reviews all core API changes before merge
- **Test coverage gate:** Agent Smith reviews test implementations for edge cases
- **Documentation gate:** Morpheus reviews user-facing docs for clarity and completeness
- **Release gate:** Neo approves all publishing workflows

## Rejection Lockout

If a reviewer rejects work:
1. Original author is locked out of the revision
2. Lead (Neo) selects a different agent for the revision
3. Locked-out author may not contribute to the revision in any form
