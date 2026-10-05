// TC2 browser glue (D2-009, 12 §3): an IndexedDB save store with fixed names (independent of the page URL),
// page visibility events for saving on leave, the real clock, URL parameters, and a test bridge
// (window.__TC2 state for Playwright, window.__TC2_CMDS command queue). Calls back into C# with SendMessage.
var TC2Lib = {
  $TC2: {
    db: null,
    opening: false,
    waiters: [],
    loaded: {},
    open: function (ok, fail) {
      if (TC2.db) { ok(TC2.db); return; }
      TC2.waiters.push([ok, fail]);
      if (TC2.opening) return;
      TC2.opening = true;
      var finish = function (db, err) {
        TC2.opening = false;
        var w = TC2.waiters; TC2.waiters = [];
        for (var i = 0; i < w.length; i++) { if (db) w[i][0](db); else w[i][1](err); }
      };
      try {
        if (!window.indexedDB) { finish(null, 'no indexedDB'); return; }
        var rq = window.indexedDB.open('tiny-consequences-2', 1);
        rq.onupgradeneeded = function () { rq.result.createObjectStore('saves'); };
        rq.onsuccess = function () { TC2.db = rq.result; finish(TC2.db, null); };
        rq.onerror = function () { finish(null, 'open failed'); };
        rq.onblocked = function () { finish(null, 'blocked'); };
      } catch (e) { finish(null, String(e)); }
      try { if (navigator.storage && navigator.storage.persist) navigator.storage.persist(); } catch (e) { }
    },
    send: function (method, value) {
      try { SendMessage('TC2', method, value); } catch (e) { console.error('TC2 SendMessage failed', e); }
    }
  },

  TC2_IdbSave: function (keyPtr, dataPtr, len) {
    var key = UTF8ToString(keyPtr);
    var bytes = HEAPU8.slice(dataPtr, dataPtr + len);
    TC2.open(function (db) {
      try {
        var tx = db.transaction('saves', 'readwrite');
        tx.objectStore('saves').put(bytes, key);
        tx.oncomplete = function () { TC2.send('OnIdbSaved', key + '|ok'); };
        tx.onerror = function () { TC2.send('OnIdbSaved', key + '|error'); };
      } catch (e) { TC2.send('OnIdbSaved', key + '|error'); }
    }, function (err) { TC2.send('OnIdbSaved', key + '|error'); });
  },

  TC2_IdbLoad: function (keyPtr) {
    var key = UTF8ToString(keyPtr);
    TC2.open(function (db) {
      try {
        var tx = db.transaction('saves', 'readonly');
        var rq = tx.objectStore('saves').get(key);
        rq.onsuccess = function () {
          var v = rq.result;
          if (v) { TC2.loaded[key] = new Uint8Array(v); TC2.send('OnIdbLoaded', key + '|' + TC2.loaded[key].length); }
          else TC2.send('OnIdbLoaded', key + '|0');
        };
        rq.onerror = function () { TC2.send('OnIdbLoaded', key + '|-1'); };
      } catch (e) { TC2.send('OnIdbLoaded', key + '|-1'); }
    }, function (err) { TC2.send('OnIdbLoaded', key + '|-1'); });
  },

  TC2_IdbTake: function (keyPtr, destPtr, len) {
    var key = UTF8ToString(keyPtr);
    var b = TC2.loaded[key];
    if (!b) return 0;
    HEAPU8.set(b.subarray(0, len), destPtr);
    delete TC2.loaded[key];
    return 1;
  },

  TC2_IdbDelete: function (keyPtr) {
    var key = UTF8ToString(keyPtr);
    TC2.open(function (db) {
      try { var tx = db.transaction('saves', 'readwrite'); tx.objectStore('saves').delete(key); } catch (e) { }
    }, function () { });
  },

  TC2_NowMs: function () { return Date.now(); },

  TC2_UrlParam: function (namePtr) {
    var name = UTF8ToString(namePtr);
    var v = null;
    try { v = new URLSearchParams(window.location.search).get(name); } catch (e) { }
    if (v === null) v = '';
    var n = lengthBytesUTF8(v) + 1;
    var buf = _malloc(n);
    stringToUTF8(v, buf, n);
    return buf;
  },

  TC2_RegisterPageEvents: function () {
    document.addEventListener('visibilitychange', function () { TC2.send('OnVisibility', document.visibilityState); });
    window.addEventListener('pagehide', function () { TC2.send('OnVisibility', 'hidden'); });
  },

  TC2_PostState: function (jsonPtr) {
    try { window.__TC2 = JSON.parse(UTF8ToString(jsonPtr)); } catch (e) { window.__TC2_ERR = String(e); }
  },

  TC2_PollCommand: function () {
    var q = window.__TC2_CMDS;
    var s = (q && q.length) ? String(q.shift()) : '';
    var n = lengthBytesUTF8(s) + 1;
    var buf = _malloc(n);
    stringToUTF8(s, buf, n);
    return buf;
  },

  TC2_Log: function (msgPtr) {
    var s = UTF8ToString(msgPtr);
    try { (window.__TC2_LOG = window.__TC2_LOG || []).push(s); if (window.__TC2_LOG.length > 500) window.__TC2_LOG.shift(); } catch (e) { }
  }
};

autoAddDeps(TC2Lib, '$TC2');
mergeInto(LibraryManager.library, TC2Lib);
