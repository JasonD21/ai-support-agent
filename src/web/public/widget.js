(function () {
  if (window.__asaWidgetLoaded) return;
  window.__asaWidgetLoaded = true;

  var script = document.currentScript;
  if (!script) {
    var all = document.querySelectorAll("script[data-site-key]");
    script = all[all.length - 1];
  }
  if (!script) return;

  var siteKey = script.getAttribute("data-site-key");
  if (!siteKey) return;

  var appBase;
  try {
    appBase = new URL(script.src).origin;
  } catch (e) {
    return;
  }

  var hostOrigin = window.location.origin;
  var position = script.getAttribute("data-position") || "bottom-right";
  var side = position.indexOf("left") !== -1 ? "left" : "right";

  var open = false;
  var iframe = null;

  var bubble = document.createElement("button");
  bubble.setAttribute("aria-label", "Open chat");
  bubble.innerHTML = chatIcon();
  bubble.style.cssText =
    "position:fixed;bottom:20px;" +
    side +
    ":20px;width:56px;height:56px;border-radius:50%;" +
    "background:#4f46e5;color:#fff;border:none;cursor:pointer;box-shadow:0 4px 14px rgba(0,0,0,.25);" +
    "display:flex;align-items:center;justify-content:center;z-index:2147483646;padding:0;";
  bubble.addEventListener("click", toggle);
  document.body.appendChild(bubble);

  function toggle() {
    open = !open;
    if (open) {
      if (!iframe) iframe = createIframe();
      iframe.style.display = "block";
      bubble.innerHTML = closeIcon();
    } else {
      if (iframe) iframe.style.display = "none";
      bubble.innerHTML = chatIcon();
    }
  }

  function createIframe() {
    var f = document.createElement("iframe");
    f.src =
      appBase +
      "/embed?siteKey=" +
      encodeURIComponent(siteKey) +
      "&origin=" +
      encodeURIComponent(hostOrigin);
    f.title = "Support chat";
    f.style.cssText =
      "position:fixed;bottom:88px;" +
      side +
      ":20px;width:380px;height:560px;" +
      "max-width:calc(100vw - 40px);max-height:calc(100vh - 120px);border:none;border-radius:16px;" +
      "overflow:hidden;display:none;box-shadow:0 8px 30px rgba(0,0,0,.25);z-index:2147483646;background:#fff;";
    document.body.appendChild(f);
    return f;
  }

  window.addEventListener("message", function (e) {
    if (e.origin !== appBase) return;
    if (e.data && e.data.type === "asa:close") toggle();
  });

  function chatIcon() {
    return '<svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>';
  }
  function closeIcon() {
    return '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>';
  }
})();
