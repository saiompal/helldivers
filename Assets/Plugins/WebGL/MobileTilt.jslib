

mergeInto(LibraryManager.library, {
    StartTiltSensor: function () {
        if (window.unityTiltStarted) return;

        window.unityTiltValue = 0;

        function startListening() {
            if (window.unityTiltStarted) return;
            window.unityTiltStarted = true;

            var neutral = null;

            window.addEventListener("deviceorientation", function (e) {
                var angle = screen.orientation
                    ? screen.orientation.angle
                    : (window.orientation || 0);

                var raw = Math.abs(angle) === 90
                    ? (angle === 90 ? e.beta : -e.beta)
                    : e.gamma;

                if (raw == null) return;

                if (neutral === null) neutral = raw;

                var difference = raw - neutral;

                if (difference > 180) difference -= 360;
                if (difference < -180) difference += 360;

                if (Math.abs(difference) < 4) {
                    window.unityTiltValue = 0;
                } else {
                    window.unityTiltValue = Math.max(
                        -1, Math.min(1, difference / 25)
                    );
                }
            });
        }

        if (typeof DeviceOrientationEvent !== "undefined" &&
            typeof DeviceOrientationEvent.requestPermission === "function") {

            var button = document.createElement("button");
            button.textContent = "Enable Tilt Controls";
            button.style.cssText =
                "position:fixed;top:20px;left:20px;" +
                "z-index:99999;padding:14px;font-size:16px;";

            document.body.appendChild(button);

            button.onclick = function () {
                button.disabled = true;

                DeviceOrientationEvent.requestPermission()
                    .then(function (result) {
                        if (result === "granted") {
                            startListening();
                            button.remove();
                        } else {
                            button.textContent = "Permission denied";
                            button.disabled = false;
                        }
                    })
                    .catch(function (error) {
                        console.error("Tilt permission error:", error);
                        button.textContent = "Enable Tilt Controls";
                        button.disabled = false;
                    });
            };
        } else {
            startListening();
        }
    },

    GetTiltValue: function () {
        return window.unityTiltValue || 0;
    }
});
