window.doclyTheme = {
  key: "docly.theme",

  get: function () {
    try { return localStorage.getItem(this.key); } catch { return null; }
  },

  set: function (theme) {
    try { localStorage.setItem(this.key, theme); } catch { /* storage blocked */ }
    document.documentElement.setAttribute("data-theme", theme);
  },

  init: function () {
    const saved = this.get();
    const preferred = saved || (window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
    document.documentElement.setAttribute("data-theme", preferred);
    return preferred;
  }
};

window.doclyTheme.init();

window.doclyDom = {
  scrollToBottom: function (element, smooth) {
    if (!element) return;
    element.scrollTo({ top: element.scrollHeight, behavior: smooth ? "smooth" : "auto" });
  },

  autoResize: function (element) {
    if (!element) return;
    element.style.height = "auto";
    element.style.height = Math.min(element.scrollHeight, 220) + "px";
  },

  focus: function (element) {
    if (element) element.focus();
  },

  copyText: async function (text) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      return false;
    }
  },

  // Blazor can't conditionally preventDefault, so stop the newline that Enter would
  // insert after the message is sent (Shift+Enter and IME composition still work).
  wireComposer: function (textarea) {
    if (!textarea || textarea.dataset.doclyWired) return;
    textarea.dataset.doclyWired = "true";
    textarea.addEventListener("keydown", function (e) {
      if (e.key === "Enter" && !e.shiftKey && !e.isComposing) {
        e.preventDefault();
      }
    });
  },

  // Blazor's InputFile has no native drag-and-drop support, and DragEventArgs.DataTransfer.Files
  // only exposes filenames (string[]) to .NET, not real file content. Bridge it natively instead:
  // set the dropped FileList directly on the hidden <input type=file> and dispatch a change event,
  // which Blazor's InputFile OnChange handler picks up with real IBrowserFile objects.
  wireDropZone: function (dropZoneId, inputId) {
    const dropZone = document.getElementById(dropZoneId);
    const input = document.getElementById(inputId);
    if (!dropZone || !input || dropZone.dataset.doclyWired) return;
    dropZone.dataset.doclyWired = "true";

    dropZone.addEventListener("drop", function (e) {
      e.preventDefault();
      e.stopPropagation();
      if (e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files.length > 0) {
        input.files = e.dataTransfer.files;
        input.dispatchEvent(new Event("change", { bubbles: true }));
      }
    });
  }
};
