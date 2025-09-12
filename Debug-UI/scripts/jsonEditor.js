// Global değişkenler
let requestEditor, responseEditor;

// Sayfa yüklendiğinde çalışacak
window.onload = function () {
  // JSON Editor'ları oluştur
  requestEditor = new JSONEditor(document.getElementById("requestEditor"), {
    mode: "code", // Başlangıçta code modunda başlat
    modes: ["code", "tree", "form", "text", "view"],
    onError: function (err) {
      console.error(err);
    },
  });

  responseEditor = new JSONEditor(document.getElementById("responseEditor"), {
    mode: "code", // Başlangıçta code modunda başlat
    modes: ["code", "tree", "form", "text", "view"],
    onError: function (err) {
      console.error(err);
    },
  });

  // Textarea'lardan değerleri al ve JSON Editor'lara aktar
  const requestTextarea = document.getElementById("request");
  const responseTextarea = document.getElementById("response");

  if (requestTextarea && requestTextarea.value) {
    try {
      requestEditor.set(JSON.parse(requestTextarea.value));
    } catch (e) {
      console.error("Request JSON parse error:", e);
    }
  }

  if (responseTextarea && responseTextarea.value) {
    try {
      responseEditor.set(JSON.parse(responseTextarea.value));
    } catch (e) {
      console.error("Response JSON parse error:", e);
    }
  }
};
