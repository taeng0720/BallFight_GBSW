mergeInto(LibraryManager.library, {
  HBF_SelectImage: function(receiverPointer) {
    var receiver = UTF8ToString(receiverPointer);
    var input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/png,image/jpeg,image/webp';
    input.style.display = 'none';
    document.body.appendChild(input);
    var finish = function(payload) { SendMessage(receiver, 'ReceiveImage', JSON.stringify(payload)); input.remove(); };
    input.addEventListener('cancel', function() { input.remove(); }, { once: true });
    input.addEventListener('change', function() {
      var file = input.files && input.files[0];
      if (!file) { input.remove(); return; }
      if (file.size > 4194304) { finish({ error: '원본 이미지는 4MB 이하여야 합니다.' }); return; }
      var url = URL.createObjectURL(file);
      var picture = new Image();
      picture.onerror = function() { URL.revokeObjectURL(url); finish({ error: '이미지를 읽을 수 없습니다.' }); };
      picture.onload = function() {
        var scale = Math.min(1, 256 / Math.max(picture.width, picture.height));
        var canvas = document.createElement('canvas');
        canvas.width = Math.max(1, Math.round(picture.width * scale));
        canvas.height = Math.max(1, Math.round(picture.height * scale));
        canvas.getContext('2d').drawImage(picture, 0, 0, canvas.width, canvas.height);
        var data = canvas.toDataURL('image/png').split(',')[1];
        URL.revokeObjectURL(url);
        if (data.length > 133336) finish({ error: '압축된 PNG가 100KB를 넘습니다. 더 단순한 이미지를 선택하세요.' });
        else finish({ data: data });
      };
      picture.src = url;
    }, { once: true });
    input.click();
  }
});
