# Queue Race Fix Agent Guidance

Agents implementing this bundle must follow the existing Miautrix Mail Server architecture:

- Domain depends on nothing.
- Application depends only on Domain.
- Controllers and workers stay thin.
- Queue ownership must be explicit; no defaulted direction at enqueue boundaries.
- Production migration must be forward-only and additive.
