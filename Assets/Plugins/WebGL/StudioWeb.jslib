// Мост браузер <-> Unity для веб-версии: выбор картинки с диска, скачивание файлов, сохранение IndexedDB.
mergeInto(LibraryManager.library, {
  StudioPickImage: function (goName, method, accept) {
    var go = UTF8ToString(goName);
    var m = UTF8ToString(method);
    var input = document.createElement('input');
    input.type = 'file';
    input.accept = UTF8ToString(accept);
    input.style.display = 'none';
    input.onchange = function () {
      var f = input.files && input.files[0];
      if (!f) return;
      var r = new FileReader();
      r.onload = function () {
        var b64 = String(r.result).split(',')[1] || '';
        SendMessage(go, m, f.name + '|' + b64);
      };
      r.readAsDataURL(f);
      input.remove();
    };
    document.body.appendChild(input);
    input.click();
  },

  StudioPickText: function (goName, method, accept) {
    var go = UTF8ToString(goName);
    var m = UTF8ToString(method);
    var input = document.createElement('input');
    input.type = 'file';
    input.accept = UTF8ToString(accept);
    input.style.display = 'none';
    input.onchange = function () {
      var f = input.files && input.files[0];
      if (!f) return;
      var r = new FileReader();
      r.onload = function () { SendMessage(go, m, String(r.result)); };
      r.readAsText(f);
      input.remove();
    };
    document.body.appendChild(input);
    input.click();
  },

  StudioDownload: function (name, ptr, len, mime) {
    var bytes = HEAPU8.slice(ptr, ptr + len);
    var blob = new Blob([bytes], { type: UTF8ToString(mime) });
    var a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = UTF8ToString(name);
    document.body.appendChild(a);
    a.click();
    setTimeout(function () { URL.revokeObjectURL(a.href); a.remove(); }, 1000);
  },

  StudioSyncFS: function () {
    if (typeof FS !== 'undefined' && FS.syncfs) {
      FS.syncfs(false, function (err) { if (err) console.warn('syncfs', err); });
    }
  }
});
