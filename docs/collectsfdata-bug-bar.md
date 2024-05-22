# CollectSFData Bug Bar

## Overview

This information is required for SDL compliance. Review the [SDL Security Bug Bar Reference](https://aka.ms/sdlbugbar) for more information.

### Critical Security Vulnerabilities

- Critical-severity vulnerabilities must be evaluated and fixed based on the risk they pose to users.
- All discovered critical vulnerabilities must be patched before release.
- Examples:
  - Remote code execution
  - Privilege escalation

### High Security Vulnerabilities

- High-severity vulnerabilities must be evaluated and fixed based on the risk they pose to users.
- All discovered critical vulnerabilities must be patched before release.
- Examples:
  - Information Disclosure / PII leaks
  - Denial of service
  - Failure to encrypt sensitive data
  - Spoofing
  - Tampering

### Medium Severity

- Medium-severity vulnerabilities must be evaluated and fixed based on the risk they pose to users.
- The application must not crash under normal operations and should handle exceptions gracefully.
- Examples:
  - Data leaks
  - Application Exceptions
  - Authentication Errors
  - Security features not implemented
  - Failure to handle exceptions
  - Failure to handle user input
  - Failure to execute

### Low Severity and Quality Bugs

- Low-severity should be documented and prioritized for future releases.
- Examples:
  - Minor UI issues
  - Minor performance issues
  - Minor functional issues

### Compliance and Standards

- The application must comply with relevant industry security standards (e.g., SSL/TLS, MSAL, PII).
- Code must adhere to internal coding standards and best practices as defined in SDL.

### Performance Benchmarks

- The application should meet predefined performance benchmarks, such as response times and resource usage.

### User Data Protection

- Ensure all user data is handled securely, following data protection laws (like GDPR).

### Audit and Logging

- The application must have adequate logging for debugging and audit trails and not log security or other PII information.

### Testing and Documentation

- The application must pass all unit and integration tests.
- Documentation should be complete and up to date.

### Third-Party Dependencies

- All third-party libraries and dependencies must be up to date and free from known vulnerabilities.
- Third-party libraries and dependencies must use Central Configuration Store.

### Review and Approval

- The final version of the application must be reviewed and approved by multiple approvers before release.
