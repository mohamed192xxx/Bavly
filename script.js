const loginPanel = document.getElementById("loginPanel");
const dashboard = document.getElementById("dashboard");
const loginForm = document.getElementById("loginForm");
const welcomeName = document.getElementById("welcomeName");
const logoutButton = document.getElementById("logout");
const egyptTime = document.getElementById("egyptTime");

let timer = null;

function renderEgyptTime() {
  const now = new Date();
  egyptTime.textContent = now.toLocaleTimeString("ar-EG", {
    timeZone: "Africa/Cairo",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
}

function startClock() {
  renderEgyptTime();
  timer = setInterval(renderEgyptTime, 1000);
}

function stopClock() {
  if (!timer) return;
  clearInterval(timer);
  timer = null;
}

loginForm.addEventListener("submit", (event) => {
  event.preventDefault();

  const name = document.getElementById("name").value.trim();
  if (!name) return;

  welcomeName.textContent = name;
  loginPanel.classList.add("hidden");
  dashboard.classList.remove("hidden");
  startClock();
});

logoutButton.addEventListener("click", () => {
  loginForm.reset();
  dashboard.classList.add("hidden");
  loginPanel.classList.remove("hidden");
  stopClock();
});
