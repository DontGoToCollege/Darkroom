// Self-check run by `Darkroom.exe --smoke` (see RunSmokeAsync in MainWindow.xaml.cs). Drives the editor through its
// own controls and returns a JSON report; anything that goes wrong lands in `errors`.
(async () => {
  const $ = s => document.querySelector(s), sleep = ms => new Promise(r => setTimeout(r, ms));
  const until = async (f, ms = 5000) => { for (let t = 0; t < ms && !f(); t += 50) await sleep(50); return f(); };
  const info = () => $('#docinfo').textContent, errors = [];
  const v = $('#view');
  const drag = (x0, y0, x1, y1) => {
    const r = v.getBoundingClientRect(), cx = r.left + r.width / 2, cy = r.top + r.height / 2;
    const ev = (t, x, y) => v.dispatchEvent(new PointerEvent(t, { bubbles: true, pointerId: 1, button: 0, buttons: 1, clientX: cx + x, clientY: cy + y, isPrimary: true }));
    ev('pointerdown', x0, y0); ev('pointermove', x1, y1); ev('pointerup', x1, y1);
  };
  try {
    // Image size with a shape on the canvas (it crashed halfway in 2.0.0 and 2.0.1).
    $('#dNew').showModal(); $('#dNew').close('ok');
    if (!await until(() => info().startsWith('1200'))) throw new Error('new canvas did not open');
    $('.tool[data-tool="shape"]').click(); drag(-60, -40, 60, 40);
    if (!await until(() => document.querySelectorAll('#layerList .lname').length === 2)) throw new Error('shape was not drawn');
    $('#dResize').showModal(); $('#rLock').checked = false; $('#rW').value = 600; $('#rH').value = 400; $('#dResize').close('ok');
    if (!await until(() => info().startsWith('600 × 400'))) throw new Error('image size failed: ' + info());

    // Content-aware fill finishes, and the window keeps running while it works.
    [...document.querySelectorAll('#layerList .layer')].find(li => li.textContent.includes('Background')).click();
    $('.tool[data-tool="marquee"]').click(); drag(-20, -20, 20, 20);
    if (!await until(() => !$('[data-cmd="contentAware"]').disabled)) throw new Error('marquee made no selection');
    $('[data-cmd="contentAware"]').click();
    if (!await until(() => /fill done/.test($('#toast').textContent), 30000)) throw new Error('content-aware fill: ' + $('#toast').textContent);
  } catch (e) { errors.push(e.message); }
  errors.push(...(window.__smokeErrors || []));
  return JSON.stringify({ ver: $('#ver').textContent, errors });
})()
