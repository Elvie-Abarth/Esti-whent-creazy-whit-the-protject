// One-shot confetti burst for the order confirmation page. Plain canvas,
// no library - runs once on load, then removes itself so it doesn't sit
// around as a dead full-screen element.
(() => {
    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (reduceMotion) return;

    const canvas = document.createElement("canvas");
    canvas.style.position = "fixed";
    canvas.style.inset = "0";
    canvas.style.width = "100%";
    canvas.style.height = "100%";
    canvas.style.pointerEvents = "none";
    canvas.style.zIndex = "9999";
    document.body.appendChild(canvas);

    const ctx = canvas.getContext("2d");
    const dpr = window.devicePixelRatio || 1;
    const resize = () => {
        canvas.width = window.innerWidth * dpr;
        canvas.height = window.innerHeight * dpr;
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    };
    resize();
    window.addEventListener("resize", resize);

    const colors = ["#B83C6E", "#2C4327", "#E8C15A", "#DCE6D2", "#7A1F3D"];
    const pieceCount = 140;
    const pieces = Array.from({ length: pieceCount }, () => ({
        x: Math.random() * window.innerWidth,
        y: -20 - Math.random() * window.innerHeight * 0.5,
        size: 6 + Math.random() * 6,
        color: colors[Math.floor(Math.random() * colors.length)],
        speedY: 2 + Math.random() * 3,
        speedX: -1.5 + Math.random() * 3,
        rotation: Math.random() * 360,
        spin: -6 + Math.random() * 12,
    }));

    const durationMs = 3800;
    const startedAt = performance.now();

    function frame(now) {
        const elapsed = now - startedAt;
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        for (const p of pieces) {
            p.x += p.speedX;
            p.y += p.speedY;
            p.speedY += 0.03;
            p.rotation += p.spin;

            ctx.save();
            ctx.translate(p.x, p.y);
            ctx.rotate((p.rotation * Math.PI) / 180);
            ctx.fillStyle = p.color;
            ctx.fillRect(-p.size / 2, -p.size / 4, p.size, p.size / 2);
            ctx.restore();
        }

        if (elapsed < durationMs) {
            requestAnimationFrame(frame);
        } else {
            window.removeEventListener("resize", resize);
            canvas.remove();
        }
    }

    requestAnimationFrame(frame);
})();
