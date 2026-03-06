Here's a detailed synthesis of the provided authentication guide, approximately 100 lines, focusing on the core mechanisms and frontend integration:

---

### **Comprehensive Authentication System Guide for Frontend Integration**

This document details an authentication system using a **hybrid token strategy** for robust frontend integration with Single Page Applications (SPAs) or similar clients. It leverages JSON Web Tokens (JWT) for API access and secure HTTP-only cookies for session management.

**I. Authentication Overview**

The system employs a **Bearer token model** with two primary tokens:

1.  **Access Token (JWT):** A short-lived token used for authorizing requests to protected API endpoints. It's sent in the `Authorization: Bearer <token>` HTTP header.
2.  **Refresh Token:** A long-lived, opaque token used solely for obtaining new Access Tokens when the current one expires. It's securely managed as an **HTTP-only, Secure, SameSite cookie** by the browser.

Key features include stateless JWTs, automatic token refresh with rotation, Google OAuth integration, role-based authorization, and full cross-origin support.

**II. API Endpoints (`/api/Auth/` Prefix)**

All authentication actions are handled through specific backend endpoints:

1.  **`POST /Register`**:
    *   **Purpose:** Create a new user account with email and password.
    *   **Request:** `{"email": "...", "password": "..."}`
    *   **Response:** `{"id": "...", "email": "...", "userName": "...", "role": "..."}`. The server also **sets an HTTP-only Refresh Token cookie**.
    *   **Note:** User is automatically signed in.

2.  **`POST /LogIn`**:
    *   **Purpose:** Authenticate an existing user with email and password.
    *   **Request:** `{"email": "...", "password": "..."}`
    *   **Response:** `{"id": "...", "email": "...", "userName": "...", "token": "...", "role": "..."}`. The `token` field contains the **Access Token**. The server also **sets an HTTP-only Refresh Token cookie**.

3.  **`POST /GoogleLogin`**:
    *   **Purpose:** Authenticate/register users using Google OAuth.
    *   **Request:** `{"credential": "google_jwt_credential_token"}`
    *   **Response:** `{"token": "...", "email": "...", "name": "...", "picture": "...", "role": "..."}`. Returns the **Access Token** and user profile data. The server **sets an HTTP-only Refresh Token cookie**.

4.  **`GET /RefreshToken`**:
    *   **Purpose:** Obtain a new Access Token.
    *   **Mechanism:** The browser automatically sends the existing `HttpOnly` Refresh Token cookie. No explicit `Authorization` header is needed.
    *   **Response:** `{"accessToken": "..."}`. Returns a **new Access Token**. The server also **rotates the refresh token** by setting a *new* `HttpOnly` cookie.

5.  **`GET /Current`**:
    *   **Purpose:** Retrieve the authenticated user's detailed profile information.
    *   **Mechanism:** Requires a valid **Access Token** in the `Authorization: Bearer` header.
    *   **Response:** `{"id": "...", "email": "...", "userName": "...", "role": "..."}`. This data is fetched from the backend database, ensuring freshness and completeness.

6.  **`DELETE /Logout`**:
    *   **Purpose:** Invalidate the current user session.
    *   **Mechanism:** Requires a valid Access Token. The server **clears the Refresh Token cookie** and invalidates the session.
    *   **Response:** Empty successful response `{}`. The frontend should clear its stored Access Token.

**III. Authentication Flow**

*   **Initial Login:**
    1.  Frontend sends credentials to `/LogIn` (or `/Register`, `/GoogleLogin`).
    2.  Backend validates, generates tokens, returns **Access Token in body**, and sets **Refresh Token as `HttpOnly` cookie**.
    3.  Frontend stores the Access Token **(example uses `localStorage` for Access Token)** and uses it for subsequent API calls.

*   **Token Refresh:**
    1.  Frontend makes an API call with its Access Token.
    2.  If the Access Token is expired (401 Unauthorized), the frontend's interceptor (e.g., `authenticatedFetch`) triggers.
    3.  Frontend calls `/RefreshToken`. The browser automatically sends the `HttpOnly` Refresh Token cookie.
    4.  Backend validates the refresh token, issues a **new Access Token (in body)**, and sets a **new `HttpOnly` Refresh Token cookie**.
    5.  Frontend updates its stored Access Token and retries the original failed API call.

**IV. Frontend Implementation (React/TypeScript Example Details)**

*   **`AuthService` Class:** A central service to abstract all authentication logic.
    *   Handles `login`, `register`, `googleLogin`, `refreshToken`, `getCurrentUser`, `logout` methods.
    *   Manages the `accessToken` in `localStorage` (as per the example, though in-memory is generally safer for short-lived tokens).
    *   Includes `authenticatedFetch`: An interceptor that automatically adds the `Authorization: Bearer` header and handles 401 errors by attempting to refresh the token and retry the request.
    *   `credentials: 'include'` is crucial for all `fetch` calls to ensure cookies (especially the `HttpOnly` Refresh Token) are sent.
*   **`useAuth` Hook:** Provides a React Context for easy access to authentication state (`user`, `isAuthenticated`, `isLoading`) and actions (`login`, `logout`, etc.) throughout the component tree.
*   **User Profile Data:** The login/register responses return basic user profile data (ID, email, name, role) which the frontend can store (e.g., in `localStorage` or `user` state) for immediate display, reducing calls to `/Current`. The `/Current` endpoint provides the latest, full profile.

**V. Security Considerations**

*   **Token Storage:** The guide's example uses `localStorage` for the Access Token. While convenient for persistence, this is vulnerable to XSS. **Relying on a short Access Token lifespan and strong XSS prevention is critical.** Refresh Tokens are securely protected by `HttpOnly` cookies.
*   **Password Handling:** All communication **must** be over HTTPS.
*   **CSRF Protection:** While `SameSite` cookies mitigate some CSRF, explicit anti-CSRF tokens are recommended for endpoints that use `HttpOnly` cookies and modify state (e.g., a "revoke session" endpoint, though not explicitly shown in this guide, would interact with the refresh token).
*   **CORS:** Backend must explicitly configure CORS to allow your frontend's origin(s) and allow `credentials: true`.
*   **Error Handling:** Generic error messages are returned to avoid exposing sensitive backend details.