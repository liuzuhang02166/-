(function () {
  function postToParent(type, payload) {
    try {
      payload = payload || {};
      if (window.parent && window.parent !== window) {
        window.parent.postMessage({ source: "schedulerapp-proto", type: type, payload: payload }, "*");
      }
    } catch {}
  }

  function onMessage(handler) {
    window.addEventListener("message", function (event) {
      if (!event || !event.data || event.data.source !== "schedulerapp-proto") return;
      handler(event.data);
    });
  }

  function qs(selector, root) {
    root = root || document;
    return root.querySelector(selector);
  }

  function qsa(selector, root) {
    root = root || document;
    return Array.prototype.slice.call(root.querySelectorAll(selector));
  }

  function show(el) {
    if (!el) return;
    el.classList.remove("hidden");
    el.classList.add("flex");
  }

  function hide(el) {
    if (!el) return;
    el.classList.add("hidden");
    el.classList.remove("flex");
  }

  window.ProtoUI = {
    postToParent: postToParent,
    onMessage: onMessage,
    qs: qs,
    qsa: qsa,
    show: show,
    hide: hide
  };
})();
