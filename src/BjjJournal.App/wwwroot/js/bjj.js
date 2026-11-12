// Única pieza en JavaScript: acceso a localStorage, descarga de ficheros y tema. Toda la lógica está en C#.
window.bjjStorage = {
  get: (key) => { try { return localStorage.getItem(key); } catch { return null; } },
  set: (key, value) => { try { if (value === null || value === undefined) localStorage.removeItem(key); else localStorage.setItem(key, value); } catch (e) { console.warn("No se pudo guardar", e); } }
};
window.bjjFiles = {
  download: (name, mime, text) => {
    const url = URL.createObjectURL(new Blob([text], { type: mime }));
    const a = Object.assign(document.createElement("a"), { href: url, download: name });
    document.body.appendChild(a); a.click(); a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
};
window.bjjTheme = {
  get: () => { try { return localStorage.getItem("bjj-theme") || "dark"; } catch { return "dark"; } },
  set: (theme) => {
    document.documentElement.dataset.theme = theme;
    document.querySelector('meta[name="theme-color"]')?.setAttribute("content", theme === "light" ? "#f4f4f1" : "#0a0a0c");
    try { localStorage.setItem("bjj-theme", theme); } catch { }
  }
};
window.bjjTheme.set(window.bjjTheme.get());
window.bjjLang = {
  get: () => { try { return localStorage.getItem("bjj-lang") || "es"; } catch { return "es"; } },
  apply: (lang) => { document.documentElement.lang = lang; },
  set: (lang) => { try { localStorage.setItem("bjj-lang", lang); } catch { } location.reload(); }
};
