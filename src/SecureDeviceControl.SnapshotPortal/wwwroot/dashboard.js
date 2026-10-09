const menuButton = document.querySelector("#menu-button");
const drawer = document.querySelector("#pc-drawer");
const scrim = document.querySelector("#scrim");
const pcList = document.querySelector("#pc-list");
const pcTitle = document.querySelector("#pc-title");
const dateFilter = document.querySelector("#date-filter");
const shots = document.querySelector("#shots");
const statusLine = document.querySelector("#status");
const meterText = document.querySelector("#meter-text");
const meterFill = document.querySelector("#meter-fill");
const viewer = document.querySelector("#viewer");
const viewerImage = document.querySelector("#viewer-image");
const viewerDevice = document.querySelector("#viewer-device");
const viewerTime = document.querySelector("#viewer-time");
const deleteButton = document.querySelector("#delete-shot");
const logoutButton = document.querySelector("#logout");

let csrf = "";
let library = [];
let selectedFolder = "";
let activeKey = "";

menuButton.addEventListener("click", () => setMenu(!drawer.hidden ? false : true));
scrim.addEventListener("click", () => setMenu(false));
dateFilter.addEventListener("change", renderShots);
logoutButton.addEventListener("click", async () => {
  await fetch("/api/logout", { method: "POST", credentials: "same-origin" });
  window.location.assign("/");
});

deleteButton.addEventListener("click", async () => {
  if (!activeKey) {
    return;
  }
  const confirmed = window.confirm("Delete this frame from the cloud bucket? It will be gone from storage, not only from this screen.");
  if (!confirmed) {
    return;
  }
  deleteButton.disabled = true;
  try {
    const response = await fetch("/api/snapshots/delete", {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ key: activeKey, csrf })
    });
    const payload = await response.json();
    if (!response.ok) {
      statusLine.textContent = payload.error || "Delete failed.";
      return;
    }
    viewer.close();
    activeKey = "";
    await loadLibrary();
  } catch {
    statusLine.textContent = "Delete failed.";
  } finally {
    deleteButton.disabled = false;
  }
});

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape") {
    if (viewer.open) {
      viewer.close();
    } else {
      setMenu(false);
    }
  }
});

boot();

async function boot() {
  const response = await fetch("/api/session", { credentials: "same-origin" });
  const payload = await response.json();
  if (!payload.authenticated) {
    window.location.replace("/");
    return;
  }
  csrf = payload.csrf || "";
  await Promise.all([loadLibrary(), loadStorage()]);
}

function setMenu(open) {
  drawer.hidden = !open;
  scrim.hidden = !open;
  menuButton.setAttribute("aria-expanded", open ? "true" : "false");
}

async function loadStorage() {
  const response = await fetch("/api/storage", { credentials: "same-origin" });
  const payload = await response.json();
  if (!response.ok || !payload.exact || payload.usedBytes == null) {
    meterText.textContent = "Space left appears after the usage SQL is run in Supabase.";
    meterFill.style.width = "0%";
    return;
  }
  const used = Number(payload.usedBytes);
  const quota = Number(payload.quotaBytes);
  const left = Math.max(0, quota - used);
  const percent = quota > 0 ? Math.min(100, (used / quota) * 100) : 100;
  meterText.textContent = `${formatBytes(used)} used · ${formatBytes(left)} left · ${formatBytes(quota)} plan`;
  meterFill.style.width = `${percent}%`;
}

function formatBytes(bytes) {
  return `${(bytes / 1_000_000_000).toFixed(2)} GB`;
}

async function loadLibrary() {
  statusLine.textContent = "Reading the bucket…";
  const response = await fetch("/api/library", { credentials: "same-origin" });
  const payload = await response.json();
  if (!response.ok) {
    statusLine.textContent = payload.error || "The bucket could not be read.";
    library = [];
    renderPcs();
    renderShots();
    return;
  }
  library = payload.devices || [];
  if (selectedFolder && !library.some((device) => device.folder === selectedFolder)) {
    selectedFolder = "";
  }
  renderPcs();
  renderShots();
  statusLine.textContent = library.length === 0
    ? "No PCs in the bucket yet."
    : "Open the menu and pick a PC.";
}

function renderPcs() {
  pcList.replaceChildren();
  if (library.length === 0) {
    const empty = document.createElement("p");
    empty.className = "drawer-empty";
    empty.textContent = "No computers yet.";
    pcList.append(empty);
    return;
  }

  for (const device of library) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "device-link";
    if (device.folder === selectedFolder) {
      button.setAttribute("aria-current", "true");
    }
    button.innerHTML = `${escapeHtml(device.folder)}<small>${device.count} snapshot${device.count === 1 ? "" : "s"}</small>`;
    button.addEventListener("click", () => selectPc(device.folder));
    pcList.append(button);
  }
}

function selectPc(folder) {
  selectedFolder = folder;
  dateFilter.value = "";
  setMenu(false);
  renderPcs();
  renderShots();
}

function renderShots() {
  shots.replaceChildren();
  const device = library.find((item) => item.folder === selectedFolder);
  if (!device) {
    pcTitle.textContent = "Choose a PC";
    dateFilter.disabled = true;
    dateFilter.replaceChildren(new Option("All dates", ""));
    const empty = document.createElement("p");
    empty.className = "empty";
    empty.textContent = "Open the menu on the left and choose a PC.";
    shots.append(empty);
    return;
  }

  pcTitle.textContent = device.folder;
  const dates = device.days.map((day) => day.date);
  const current = dateFilter.value;
  dateFilter.replaceChildren(new Option("All dates", ""));
  for (const date of dates) {
    dateFilter.append(new Option(formatDay(date), date));
  }
  dateFilter.value = dates.includes(current) ? current : "";
  dateFilter.disabled = false;

  const frames = device.days
    .flatMap((day) => day.shots)
    .filter((shot) => !dateFilter.value || shot.capturedAt.slice(0, 10) === dateFilter.value)
    .sort((left, right) => right.capturedAt.localeCompare(left.capturedAt));

  statusLine.textContent = frames.length === 0
    ? `No snapshots for ${device.folder} on that date.`
    : `${device.folder} · ${frames.length} snapshot${frames.length === 1 ? "" : "s"}, latest first.`;

  paintFrames(device.folder, frames, 24);
}

function paintFrames(folder, frames, count) {
  shots.replaceChildren();
  const page = frames.slice(0, count);
  const grid = document.createElement("div");
  grid.className = "grid";
  for (const shot of page) {
    const card = document.createElement("button");
    card.type = "button";
    card.className = "shot";
    const image = document.createElement("img");
    image.alt = "";
    image.loading = "lazy";
    image.decoding = "async";
    image.src = `/api/media?key=${encodeURIComponent(shot.key)}`;
    const caption = document.createElement("span");
    caption.innerHTML = `<b>${escapeHtml(folder)}</b><br>${escapeHtml(formatDay(shot.capturedAt.slice(0, 10)))}<br>${escapeHtml(formatTime(shot.capturedAt))}`;
    card.append(image, caption);
    card.addEventListener("click", () => openShot(folder, shot));
    grid.append(card);
  }
  shots.append(grid);

  if (page.length < frames.length) {
    const more = document.createElement("button");
    more.type = "button";
    more.className = "stamp ghost more";
    more.textContent = `Show ${Math.min(24, frames.length - page.length)} earlier`;
    more.addEventListener("click", () => paintFrames(folder, frames, count + 24));
    shots.append(more);
  }
}

function openShot(folder, shot) {
  activeKey = shot.key;
  viewerDevice.textContent = folder;
  viewerTime.textContent = `${formatDay(shot.capturedAt.slice(0, 10))} · ${formatTime(shot.capturedAt)}`;
  viewerImage.alt = `Snapshot from ${folder}`;
  viewerImage.src = `/api/media?key=${encodeURIComponent(shot.key)}`;
  if (typeof viewer.showModal === "function") {
    viewer.showModal();
  } else {
    viewer.setAttribute("open", "");
  }
}

function formatDay(isoDate) {
  const [year, month, day] = isoDate.split("-").map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  return date.toLocaleDateString(undefined, { weekday: "short", day: "2-digit", month: "short", year: "numeric", timeZone: "UTC" });
}

function formatTime(iso) {
  return new Date(iso).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit", second: "2-digit" });
}

function escapeHtml(value) {
  return value.replace(/[&<>"']/g, (character) => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    "\"": "&quot;",
    "'": "&#39;"
  })[character]);
}
