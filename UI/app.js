const state = {
  games: [],
  categories: [],
  settings: {},
  locked: false,
  isAdminAuthenticated: false,
};

const isAdminPage = document.body.dataset.page === 'admin';

function send(type, data = {}) {
  if (!window.chrome?.webview) return;
  window.chrome.webview.postMessage({ type, data });
}

function toast(message, level = 'info') {
  const host = document.getElementById('toastHost');
  if (!host) return;
  const el = document.createElement('div');
  el.className = `toast ${level}`;
  el.textContent = message;
  host.appendChild(el);
  setTimeout(() => el.remove(), 3200);
}

function applyTheme() {
  const theme = state.settings.theme || 'dark';
  document.body.dataset.theme = theme;
  const select = document.getElementById('themeSelect');
  if (select) select.value = theme;
}

function renderLauncher() {
  if (isAdminPage) return;

  const q = (document.getElementById('searchInput')?.value || '').toLowerCase();
  const category = document.getElementById('categoryFilter')?.value || '';
  const grid = document.getElementById('gamesGrid');
  const filter = document.getElementById('categoryFilter');

  if (!grid || !filter) return;
  filter.innerHTML = '<option value="">All Categories</option>' +
    state.categories.map(c => `<option value="${c.id}">${c.name}</option>`).join('');
  filter.value = category;

  const filtered = state.games.filter(g => {
    const matchesSearch = g.name.toLowerCase().includes(q);
    const matchesCategory = !category || g.categoryId === category;
    return matchesSearch && matchesCategory;
  });

  grid.innerHTML = filtered.map(g => `
    <article class="card">
      <img loading="lazy" src="${g.imagePath || ''}" alt="${g.name}" onerror="this.src=''" />
      <div class="card-body">
        <h3>${g.name}</h3>
        <p>${g.description || ''}</p>
        <small>Launches: ${g.launchCount || 0}</small><br>
        <small>Last: ${g.lastPlayed ? new Date(g.lastPlayed).toLocaleString() : 'Never'}</small>
        <div style="margin-top:8px;">
          <button class="btn" onclick="window.play('${g.id}')">Deploy</button>
        </div>
      </div>
    </article>
  `).join('');

  const lockOverlay = document.getElementById('lockOverlay');
  const pinSetup = document.getElementById('pinSetup');
  if (lockOverlay) lockOverlay.classList.toggle('hidden', !state.locked);
  if (pinSetup) pinSetup.classList.toggle('hidden', !!state.settings.pinEnabled);
}

function renderAdmin() {
  if (!isAdminPage) return;

  const login = document.getElementById('adminLogin');
  const dashboard = document.getElementById('adminDashboard');
  if (login && dashboard) {
    login.classList.toggle('hidden', state.isAdminAuthenticated);
    dashboard.classList.toggle('hidden', !state.isAdminAuthenticated);
  }

  if (!state.isAdminAuthenticated) return;

  const categorySelect = document.getElementById('gameCategory');
  if (categorySelect) {
    categorySelect.innerHTML = '<option value="">Unassigned</option>' +
      state.categories.map(c => `<option value="${c.id}">${c.name}</option>`).join('');
  }

  const adminGames = document.getElementById('adminGames');
  if (adminGames) {
    adminGames.innerHTML = state.games.map(g => `
      <div>
        <strong>${g.name}</strong> (${g.launchCount || 0} launches)
        <button class="btn" onclick='window.editGame(${JSON.stringify(g)})'>Edit</button>
        <button class="btn" onclick="window.deleteGame('${g.id}')">Delete</button>
      </div>
    `).join('');
  }

  const adminCategories = document.getElementById('adminCategories');
  if (adminCategories) {
    adminCategories.innerHTML = state.categories.map(c => `
      <div>
        <strong>${c.name}</strong>
        <button class="btn" onclick="window.deleteCategory('${c.id}')">Delete</button>
      </div>
    `).join('');
  }
}

function bindEvents() {
  if (!isAdminPage) {
    document.getElementById('searchInput')?.addEventListener('input', renderLauncher);
    document.getElementById('categoryFilter')?.addEventListener('change', renderLauncher);
    document.getElementById('themeSelect')?.addEventListener('change', (e) => {
      send('setTheme', { theme: e.target.value });
    });
  } else {
    const form = document.getElementById('gameForm');
    form?.addEventListener('submit', (e) => {
      e.preventDefault();
      const payload = {
        id: document.getElementById('gameId').value,
        name: document.getElementById('gameName').value,
        description: document.getElementById('gameDescription').value,
        exePath: document.getElementById('exePath').value,
        imagePath: document.getElementById('imagePath').value,
        workingDirectory: document.getElementById('workingDirectory').value,
        categoryId: document.getElementById('gameCategory').value,
      };
      send(payload.id ? 'updateGame' : 'createGame', payload);
      form.reset();
    });
  }
}

window.play = (id) => send('play', { id });
window.submitPin = () => send('unlockPin', { pin: document.getElementById('pinInput').value });
window.setPin = () => send('setPin', { pin: document.getElementById('pinInput').value });
window.openAdmin = () => send('openAdmin');
window.openHome = () => send('openHome');
window.adminLogin = () => send('adminLogin', { password: document.getElementById('adminPassword').value });
window.pickExe = () => send('chooseExe');
window.pickImage = () => send('chooseImage');
window.deleteGame = (id) => send('deleteGame', { id });
window.deleteCategory = (id) => send('deleteCategory', { id });
window.addCategory = () => {
  const name = document.getElementById('newCategoryName').value;
  send('createCategory', { name });
  document.getElementById('newCategoryName').value = '';
};
window.editGame = (game) => {
  document.getElementById('gameId').value = game.id || '';
  document.getElementById('gameName').value = game.name || '';
  document.getElementById('gameDescription').value = game.description || '';
  document.getElementById('exePath').value = game.exePath || '';
  document.getElementById('imagePath').value = game.imagePath || '';
  document.getElementById('workingDirectory').value = game.workingDirectory || '';
  document.getElementById('gameCategory').value = game.categoryId || '';
};

window.chrome?.webview?.addEventListener('message', (event) => {
  const msg = event.data || {};
  if (msg.type === 'state') {
    state.games = msg.data.games || [];
    state.categories = msg.data.categories || [];
    state.settings = msg.data.settings || {};
    state.locked = !!msg.data.locked;
    applyTheme();
    renderLauncher();
    renderAdmin();
  }

  if (msg.type === 'toast') {
    toast(msg.data.message, msg.data.level);
  }

  if (msg.type === 'adminLoginResult') {
    state.isAdminAuthenticated = !!msg.data.success;
    renderAdmin();
  }

  if (msg.type === 'fileSelected') {
    const { target, value } = msg.data;
    const input = document.getElementById(target);
    if (input) input.value = value;
  }
});

bindEvents();
send('init');
