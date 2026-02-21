const loginPanel = document.getElementById("loginPanel");
const dashboard = document.getElementById("dashboard");
const loginForm = document.getElementById("loginForm");
const welcomeName = document.getElementById("welcomeName");
const logoutButton = document.getElementById("logout");

loginForm.addEventListener("submit", (event) => {
  event.preventDefault();

  const name = document.getElementById("name").value.trim();
  if (!name) return;

  welcomeName.textContent = name;
  loginPanel.classList.add("hidden");
  dashboard.classList.remove("hidden");
});

logoutButton.addEventListener("click", () => {
  loginForm.reset();
  dashboard.classList.add("hidden");
  loginPanel.classList.remove("hidden");
});
