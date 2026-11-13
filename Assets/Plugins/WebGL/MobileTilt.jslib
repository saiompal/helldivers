mergeInto(LibraryManager.library, {
    $isMobileBrowser: function () {
        return !!((navigator.userAgentData && navigator.userAgentData.mobile) ||
            /Android|iPhone|iPad|iPod/i.test(navigator.userAgent || "") ||
            (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1));
    },

    IsMobileBrowser__deps: ["$isMobileBrowser"],
    IsMobileBrowser: function () {
        return isMobileBrowser() ? 1 : 0;
    },

    StartTiltSensor__deps: ["$isMobileBrowser"],
    StartTiltSensor: function () {

        // A narrow browser window or a touchscreen laptop alone is not a phone.
        if (!isMobileBrowser() || window.unityTiltState) return;

        var state = window.unityTiltState = {
            listening: false, pending: false, received: false,
            neutral: null, angle: null, timer: null
        };
        window.unityTiltValue = 0;
        // These settings are intentionally grouped here for tuning.
        var settings = { deadZoneDegrees: 4, fullTiltDegrees: 25, invert: false };

        function screenAngle() {
            var angle = screen.orientation && typeof screen.orientation.angle === "number"
                ? screen.orientation.angle : (window.orientation || 0);
            return ((angle % 360) + 360) % 360;
        }

        var button = document.createElement("button");
        button.id = "unity-tilt-controls";
        button.type = "button";
        button.textContent = "Enable Tilt Controls";
        button.style.cssText =
            "position:fixed;top:calc(env(safe-area-inset-top,0px) + 12px);" +
            "left:calc(env(safe-area-inset-left,0px) + 12px);z-index:99999;" +
            "max-width:calc(100vw - env(safe-area-inset-left,0px) - env(safe-area-inset-right,0px) - 24px);" +
            "min-height:44px;padding:10px 14px;border:1px solid #75d4ff;border-radius:12px;" +
            "background:#101822;color:#f2f2f4;font:600 clamp(12px,3.5vw,16px)/1.3 Arial,sans-serif;" +
            "white-space:normal;touch-action:manipulation;";
        document.body.appendChild(button);

        function resetNeutral() {
            state.neutral = null;
            state.angle = screenAngle();
            window.unityTiltValue = 0;
        }

        function waitForReading() {
            clearTimeout(state.timer);
            state.received = false;
            resetNeutral();
            button.textContent = "Hold comfortably — calibrating";
            button.disabled = false;
            state.timer = setTimeout(function () {
                if (!state.received) button.textContent = "No tilt data — retry";
            }, 3000);
        }

        function startListening() {
            if (!state.listening) {
                state.listening = true;
                window.addEventListener("deviceorientation", function (event) {
                    var angle = screenAngle();
                    // Choose the axis aligned with the visible screen's horizontal direction.
                    var raw;
                    switch (angle) {
                        case 90: raw = event.beta; break;
                        case 180: raw = event.gamma == null ? null : -event.gamma; break;
                        case 270: raw = event.beta == null ? null : -event.beta; break;
                        default: raw = event.gamma; break;
                    }
                    if (typeof raw !== "number" || !Number.isFinite(raw)) return;
                    if (angle !== state.angle) resetNeutral();
                    if (state.neutral === null) state.neutral = raw;
                    if (!state.received) {
                        state.received = true;
                        clearTimeout(state.timer);
                        button.textContent = "Recenter Tilt";
                    }

                    // Wrap the difference so crossing -180/180 does not cause a jump.
                    var difference = ((raw - state.neutral + 540) % 360) - 180;
                    var magnitude = Math.abs(difference);
                    var strength = magnitude <= settings.deadZoneDegrees ? 0 :
                        Math.min(1, (magnitude - settings.deadZoneDegrees) /
                            (settings.fullTiltDegrees - settings.deadZoneDegrees));
                    window.unityTiltValue = Math.sign(difference) * strength *
                        (settings.invert ? -1 : 1);
                });
                window.addEventListener("orientationchange", resetNeutral);
                if (screen.orientation && screen.orientation.addEventListener)
                    screen.orientation.addEventListener("change", resetNeutral);
                document.addEventListener("visibilitychange", function () {
                    // Never reuse steering from before the browser went into the background.
                    resetNeutral();
                });
            }
            waitForReading();
        }

        button.onclick = function () {
            if (state.pending) return;
            if (state.listening) {
                waitForReading();
                return;
            }
            if (!window.isSecureContext || typeof DeviceOrientationEvent === "undefined") {
                button.textContent = "Tilt unavailable in this browser";
                button.disabled = true;
                return;
            }
            if (typeof DeviceOrientationEvent.requestPermission !== "function") {
                startListening();
                return;
            }

            // Safari requires this call directly inside the user's click handler.
            state.pending = true;
            button.disabled = true;
            function failed(error) {
                if (error) console.error("Tilt permission error:", error);
                state.pending = false;
                button.disabled = false;
                button.textContent = "Tilt permission denied — retry";
            }
            try {
                DeviceOrientationEvent.requestPermission().then(function (result) {
                    state.pending = false;
                    button.disabled = false;
                    if (result === "granted") startListening();
                    else failed();
                }).catch(failed);
            } catch (error) {
                failed(error);
            }
        };
    },

    GetTiltValue: function () {
        return window.unityTiltValue || 0;
    }
});
