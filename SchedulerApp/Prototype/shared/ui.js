export function postToParent(type, payload = {}) {
  try {
    if (window.parent && window.parent !== window) {
      window.parent.postMessage({ source: "schedulerapp-proto", type, payload }, "*");
    }
  } catch {}
}

export function onMessage(handler) {
  window.addEventListener("message", (event) => {
    if (!event?.data || event.data.source !== "schedulerapp-proto") return;
    handler(event.data);
  });
}

export function qs(selector, root = document) {
  return root.querySelector(selector);
}

export function qsa(selector, root = document) {
  return Array.from(root.querySelectorAll(selector));
}

export function show(el) {
  if (!el) return;
  el.classList.remove("hidden");
  el.classList.add("flex");
}

export function hide(el) {
  if (!el) return;
  el.classList.add("hidden");
  el.classList.remove("flex");
}
