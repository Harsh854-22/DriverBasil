const loginForm = document.querySelector("#login-form");
const passwordInput = document.querySelector("#password");
const loginError = document.querySelector("#login-error");

boot();

async function boot() {
  const response = await fetch("/api/session", { credentials: "same-origin" });
  const payload = await response.json();
  if (payload.authenticated) {
    window.location.replace("/dashboard");
    return;
  }
  passwordInput.focus();
}

loginForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  loginError.textContent = "";
  const password = passwordInput.value;
  passwordInput.value = "";
  try {
    const response = await fetch("/api/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "same-origin",
      body: JSON.stringify({ password })
    });
    const payload = await response.json();
    if (!response.ok) {
      loginError.textContent = payload.error || "Access refused.";
      return;
    }
    window.location.assign("/dashboard");
  } catch {
    loginError.textContent = "The ledger could not be reached.";
  }
});
