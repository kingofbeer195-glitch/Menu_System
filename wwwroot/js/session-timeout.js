/* Session timeout — ใช้ไฟล์เดียวกันทั้ง Blazor และ Web Forms
 *
 * - จับการใช้งาน (click / keydown / scroll / mousemove ...) แล้วบันทึกเวลาล่าสุดใน localStorage
 *   → แชร์ข้ามแท็บ/ข้ามระบบบน cptsvs552 (origin เดียวกัน) ใช้งานระบบไหนก็นับรวม
 * - มีการใช้งาน → POST keepalive (ต่อเวลา SharedSessions) ไม่ถี่กว่า pingMinutes
 * - ทุก checkSeconds → GET keepalive (เช็คว่าถูก logout จากที่อื่นหรือยัง) เฉพาะแท็บที่เปิดดูอยู่
 * - ครบ warnMinutes → popup เตือนพร้อมปุ่ม "ใช้งานต่อ"
 * - ครบ idleMinutes → ไปหน้า logout   (idleMinutes = 0 → ปิด idle logout เช่นจอ Monitor)
 * - logout จากแท็บหนึ่ง → แท็บอื่นออกตาม
 *
 * ตั้งค่าด้วย data-* บน <script> หรือ window.SessionTimeoutConfig ก่อนโหลดไฟล์:
 *   data-idle-minutes, data-warn-minutes, data-ping-minutes, data-check-seconds,
 *   data-keepalive-url, data-logout-url
 */
(function () {
    'use strict';

    if (window.SessionTimeout) return;          // กันโหลดซ้ำ

    var KEY = 'eng4_last_activity';
    var LOGOUT_KEY = 'eng4_logout_at';

    var cfg = null, tickId = null, checkId = null;
    var lastPing = 0, lastWrite = 0, memLast = Date.now(), lastMouse = 0;
    var modal = null, countdownEl = null, loggingOut = false, listening = false;

    function now() { return Date.now(); }

    function num(v, def) {
        var n = parseFloat(v);
        return isNaN(n) ? def : n;
    }

    // ---------- เวลาใช้งานล่าสุด (แชร์ผ่าน localStorage) ----------
    function getLast() {
        try {
            var v = parseInt(localStorage.getItem(KEY), 10);
            return isNaN(v) ? memLast : Math.max(v, memLast);
        } catch (e) { return memLast; }
    }

    function setLast(t, force) {
        memLast = t;
        if (!force && t - lastWrite < 5000) return;   // เขียน localStorage ไม่เกินทุก 5 วินาที
        lastWrite = t;
        try { localStorage.setItem(KEY, String(t)); } catch (e) { }
    }

    // ---------- คุยกับ server ----------
    function call(method) {
        if (!cfg.keepAliveUrl || !window.fetch) return;
        fetch(cfg.keepAliveUrl, {
            method: method,
            credentials: 'same-origin',
            cache: 'no-store',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(function (r) { if (r.status === 401) logout(); })
            .catch(function () { /* network error: ข้าม */ });
    }

    function ping() { lastPing = now(); call('POST'); }

    function check() {
        if (document.visibilityState && document.visibilityState !== 'visible') return;
        call('GET');
    }

    // ---------- การใช้งาน ----------
    function onActivity() {
        if (modal || loggingOut) return;          // ตอน popup ขึ้น ต้องกดปุ่มเท่านั้น
        var t = now();
        setLast(t, false);
        if (t - lastPing > cfg.pingMs) ping();
    }

    function onMouseMove() {
        var t = now();
        if (t - lastMouse < 2000) return;
        lastMouse = t;
        onActivity();
    }

    function continueSession() {
        setLast(now(), true);
        ping();
        hideWarn();
    }

    function logout() {
        if (loggingOut) return;
        loggingOut = true;
        try { localStorage.setItem(LOGOUT_KEY, String(now())); } catch (e) { }
        window.location.href = cfg.logoutUrl;
    }

    // ---------- popup ----------
    function fmt(ms) {
        var s = Math.max(0, Math.ceil(ms / 1000));
        var m = Math.floor(s / 60);
        s = s % 60;
        return m + ':' + (s < 10 ? '0' : '') + s;
    }

    function showWarn(remainMs) {
        if (!modal) {
            modal = document.createElement('div');
            modal.setAttribute('role', 'alertdialog');
            modal.setAttribute('aria-modal', 'true');
            modal.style.cssText =
                'position:fixed;top:0;right:0;bottom:0;left:0;z-index:2147483000;' +
                'background:rgba(0,0,0,.45);display:flex;align-items:center;justify-content:center;' +
                'font-family:inherit;';
            modal.innerHTML =
                '<div style="background:#fff;color:#263238;border-radius:12px;padding:24px 28px;' +
                'max-width:380px;width:90%;box-shadow:0 12px 32px rgba(0,0,0,.3);text-align:center;">' +
                '<div style="font-size:18px;font-weight:600;margin-bottom:8px;">Session ใกล้หมดอายุ</div>' +
                '<div style="margin-bottom:18px;line-height:1.6;">ไม่มีการใช้งานมาระยะหนึ่ง<br/>' +
                'ระบบจะออกจากระบบอัตโนมัติใน <b data-cd style="color:#C62828;font-size:18px;"></b> นาที</div>' +
                '<button type="button" data-continue style="background:#1565C0;color:#fff;border:0;' +
                'border-radius:6px;padding:9px 20px;margin:0 4px;cursor:pointer;font-size:14px;">ใช้งานต่อ</button>' +
                '<button type="button" data-logout style="background:#ECEFF1;color:#37474F;border:0;' +
                'border-radius:6px;padding:9px 20px;margin:0 4px;cursor:pointer;font-size:14px;">ออกจากระบบ</button>' +
                '</div>';
            document.body.appendChild(modal);
            countdownEl = modal.querySelector('[data-cd]');
            modal.querySelector('[data-continue]').addEventListener('click', continueSession);
            modal.querySelector('[data-logout]').addEventListener('click', logout);
            try { modal.querySelector('[data-continue]').focus(); } catch (e) { }
        }
        countdownEl.textContent = fmt(remainMs);
    }

    function hideWarn() {
        if (modal && modal.parentNode) modal.parentNode.removeChild(modal);
        modal = null;
        countdownEl = null;
    }

    function tick() {
        if (loggingOut || cfg.idleMs <= 0) return;
        var idle = now() - getLast();
        if (idle >= cfg.idleMs) logout();
        else if (idle >= cfg.warnMs) showWarn(cfg.idleMs - idle);
        else hideWarn();                           // แท็บ/ระบบอื่นมีการใช้งาน → ปิด popup
    }

    function onStorage(e) {
        if (e.key === LOGOUT_KEY && !loggingOut) logout();
    }

    // ---------- init ----------
    function readConfig(script) {
        var c = window.SessionTimeoutConfig || {};
        var d = (script && script.dataset) || {};
        return {
            idleMinutes: num(d.idleMinutes, num(c.idleMinutes, 60)),
            warnMinutes: num(d.warnMinutes, num(c.warnMinutes, 55)),
            pingMinutes: num(d.pingMinutes, num(c.pingMinutes, 5)),
            checkSeconds: num(d.checkSeconds, num(c.checkSeconds, 60)),
            keepAliveUrl: d.keepaliveUrl || c.keepAliveUrl || '',
            logoutUrl: d.logoutUrl || c.logoutUrl || '/ENG4_System/Logout?reason=timeout'
        };
    }

    function init(options) {
        stop();
        cfg = options;
        cfg.idleMs = cfg.idleMinutes * 60000;
        cfg.warnMs = Math.min(cfg.warnMinutes * 60000, cfg.idleMs);
        cfg.pingMs = cfg.pingMinutes * 60000;
        loggingOut = false;

        setLast(now(), true);                      // โหลดหน้า = มีการใช้งาน (server ต่อเวลาให้แล้ว)
        lastPing = now();

        if (!listening) {
            listening = true;
            ['click', 'keydown', 'scroll', 'touchstart', 'wheel'].forEach(function (ev) {
                window.addEventListener(ev, onActivity, { passive: true, capture: true });
            });
            window.addEventListener('mousemove', onMouseMove, { passive: true });
            window.addEventListener('storage', onStorage);
        }

        tickId = setInterval(tick, 1000);
        if (cfg.checkSeconds > 0) checkId = setInterval(check, cfg.checkSeconds * 1000);
    }

    function stop() {
        if (tickId) clearInterval(tickId);
        if (checkId) clearInterval(checkId);
        tickId = checkId = null;
        hideWarn();
    }

    window.SessionTimeout = {
        init: function (o) {
            var c = readConfig(null), k;
            for (k in (o || {})) if (Object.prototype.hasOwnProperty.call(o, k)) c[k] = o[k];
            init(c);
        },
        stop: stop,
        continueSession: continueSession
    };

    var current = document.currentScript;
    var startCfg = readConfig(current);
    if (startCfg.keepAliveUrl) {
        if (document.readyState === 'loading')
            document.addEventListener('DOMContentLoaded', function () { init(startCfg); });
        else init(startCfg);
    }
})();
