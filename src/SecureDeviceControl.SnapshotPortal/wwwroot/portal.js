const gate = document.querySelector("#gate");
const floor = document.querySelector("#floor");
const loginForm = document.querySelector("#login-form");
const passwordInput = document.querySelector("#password");
const loginError = document.querySelector("#login-error");
const deviceIndex = document.querySelector("#device-index");
const days = document.querySelector("#days");
const statusLine = document.querySelector("#status");
const filter = document.querySelector("#filter");
const viewer = document.querySelector("#viewer");
const viewerImage = document.querySelector("#viewer-image");
const viewerDevice = document.querySelector("#viewer-device");
const viewerTime = document.querySelector("#viewer-time");
const deleteButton = document.querySelector("#delete-shot");
const logoutButton = document.querySelector("#logout");

let csrf = "";
let library = [];
let activeKey = "";

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
    csrf = payload.csrf || "";
    await showFloor();
  } catch {
    loginError.textContent = "The ledger could not be reached.";
  }
});

logoutButton.addEventListener("click", async () => {
  await fetch("/api/logout", { method: "POST", credentials: "same-origin" });
  csrf = "";
  library = [];
  floor.hidden = true;
  logoutButton.hidden = true;
  gate.hidden = false;
  passwordInput.focus();
});

filter.addEventListener("input", () => render());

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
  if (event.key === "Escape" && viewer.open) {
    viewer.close();
  }
});

boot();

async function boot() {
  const response = await fetch("/api/session", { credentials: "same-origin" });
  const payload = await response.json();
  if (!payload.authenticated) {
    passwordInput.focus();
    return;
  }
  csrf = payload.csrf || "";
  await showFloor();
}

async function showFloor() {
  gate.hidden = true;
  floor.hidden = false;
  logoutButton.hidden = false;
  await loadLibrary();
}

async function loadLibrary() {
  statusLine.textContent = "Reading the bucket…";
  days.replaceChildren();
  const response = await fetch("/api/library", { credentials: "same-origin" });
  const payload = await response.json();
  if (!response.ok) {
    statusLine.textContent = payload.error || "The bucket could not be read.";
    library = [];
    render();
    return;
  }
  library = payload.devices || [];
  const total = payload.total || 0;
  statusLine.textContent = total === 0 ? "No frames in the bucket yet." : `${total} frames on file.`;
  render();
}

function render() {
  const query = filter.value.trim().toLowerCase();
  const visible = library.filter((device) => device.folder.toLowerCase().includes(query));
  deviceIndex.replaceChildren();
  days.replaceChildren();

  if (visible.length === 0) {
    const empty = document.createElement("p");
    empty.className = "empty";
    empty.textContent = library.length === 0 ? "The bucket is empty." : "No machine matches that name.";
    days.append(empty);
    return;
  }

  for (const device of visible) {
    const jump = document.createElement("button");
    jump.type = "button";
    jump.className = "device-link";
    jump.innerHTML = `${escapeHtml(device.folder)}<small>${device.count} frame${device.count === 1 ? "" : "s"}</small>`;
    jump.addEventListener("click", () => {
      document.getElementById(`device-${cssId(device.folder)}`)?.scrollIntoView({ behavior: "smooth", block: "start" });
    });
    deviceIndex.append(jump);

    const block = document.createElement("article");
    block.id = `device-${cssId(device.folder)}`;
    const title = document.createElement("h2");
    title.textContent = device.folder;
    block.append(title);

    for (const day of device.days) {
      const section = document.createElement("section");
      section.className = "day";
      const heading = document.createElement("button");
      heading.type = "button";
      heading.className = "day-toggle";
      heading.textContent = `${formatDay(day.date)} · ${day.shots.length}`;
      const grid = document.createElement("div");
      grid.className = "grid";
      grid.hidden = true;
      heading.addEventListener("click", () => {
        grid.hidden = !grid.hidden;
        if (!grid.hidden && grid.childElementCount === 0) {
          fillDay(grid, device.folder, day.shots);
        }
      });
      section.append(heading, grid);
      block.append(section);
    }
    days.append(block);
  }
}

function fillDay(grid, folder, shots) {
  const shown = Math.min(shots.length, 24 + grid.querySelectorAll(".shot").length);
  const page = shots.slice(0, shown === 0 ? 24 : shown);
  grid.replaceChildren();
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
    caption.textContent = formatTime(shot.capturedAt);
    card.append(image, caption);
    card.addEventListener("click", () => openShot(folder, shot));
    grid.append(card);
  }

  if (page.length < shots.length) {
    const more = document.createElement("button");
    more.type = "button";
    more.className = "stamp ghost more";
    more.textContent = `Show ${Math.min(24, shots.length - page.length)} more`;
    more.addEventListener("click", () => fillDay(grid, folder, shots));
    grid.append(more);
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
  return date.toLocaleDateString(undefined, { weekday: "long", day: "2-digit", month: "long", year: "numeric", timeZone: "UTC" });
}

function formatTime(iso) {
  const date = new Date(iso);
  return date.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit", second: "2-digit" });
}

function cssId(value) {
  return value.replace(/[^A-Za-z0-9_-]/g, "");
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
