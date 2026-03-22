(function () {
  function onReady(fn) {
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', fn, { once: true });
      return;
    }
    fn();
  }

  function encodeAdres(val) {
    return encodeURIComponent((val || '').trim());
  }

  function initReveal(prefersReducedMotion) {
    const revealEls = Array.from(document.querySelectorAll('.reveal'));
    if (!revealEls.length) return;

    const groupIndex = new Map();

    revealEls.forEach((el) => {
      const groupRoot =
        el.closest('.hero .row, .steps-flow, .credit-slider-grid, .pricing-secondary, .articles-grid, .row.g-4, .container') ||
        el.parentElement ||
        document.body;
      const idx = groupIndex.get(groupRoot) || 0;
      const delay = Math.min(idx * 70, 280);
      groupIndex.set(groupRoot, idx + 1);
      el.style.setProperty('--reveal-delay', delay + 'ms');
    });

    if (prefersReducedMotion || !('IntersectionObserver' in window)) {
      revealEls.forEach((el) => el.classList.add('is-visible'));
      return;
    }

    const observer = new IntersectionObserver(
      (entries, obs) => {
        entries.forEach((entry) => {
          if (!entry.isIntersecting) return;
          entry.target.classList.add('is-visible');
          obs.unobserve(entry.target);
        });
      },
      { threshold: 0.12, rootMargin: '0px 0px -10% 0px' }
    );

    revealEls.forEach((el) => observer.observe(el));
  }

  function initSurfaceGlow(prefersReducedMotion) {
    if (prefersReducedMotion) return;
    const glowEls = Array.from(document.querySelectorAll('.lux-tilt'));
    if (!glowEls.length) return;

    glowEls.forEach((el) => {
      const updateGlow = (clientX, clientY) => {
        const rect = el.getBoundingClientRect();
        const x = ((clientX - rect.left) / rect.width) * 100;
        const y = ((clientY - rect.top) / rect.height) * 100;
        el.style.setProperty('--mx', Math.max(0, Math.min(100, x)).toFixed(2) + '%');
        el.style.setProperty('--my', Math.max(0, Math.min(100, y)).toFixed(2) + '%');
      };

      el.addEventListener('pointerenter', (ev) => {
        el.classList.add('is-glow');
        updateGlow(ev.clientX, ev.clientY);
      });

      el.addEventListener('pointermove', (ev) => {
        updateGlow(ev.clientX, ev.clientY);
      });

      el.addEventListener('pointerleave', () => {
        el.classList.remove('is-glow');
      });
    });
  }

  function copyViaFallback(text) {
    return new Promise((resolve, reject) => {
      const ta = document.createElement('textarea');
      ta.value = text;
      ta.setAttribute('readonly', 'readonly');
      ta.style.position = 'fixed';
      ta.style.opacity = '0';
      ta.style.pointerEvents = 'none';
      document.body.appendChild(ta);
      ta.select();
      try {
        const ok = document.execCommand('copy');
        document.body.removeChild(ta);
        if (ok) resolve();
        else reject(new Error('copy failed'));
      } catch (err) {
        document.body.removeChild(ta);
        reject(err);
      }
    });
  }

  function initTryDemo() {
    const addrInput = document.getElementById('tryAddress');
    const reqCode = document.getElementById('tryRequest');
    const respCode = document.getElementById('tryResponse');
    const btnFetch = document.getElementById('btnFetch');
    const btnCopy = document.getElementById('btnCopy');
    if (!addrInput || !reqCode || !respCode || !btnFetch) return;

    const responseCard = respCode.closest('.card-saas');
    const responsePre = respCode.closest('pre');
    const responsePanel = document.getElementById('heroResponsePanel');
    const skeleton = document.getElementById('tryResponseSkeleton');
    const copyToast = document.getElementById('copyToast');
    let copyToastTimer = null;

    function updateRequestPreview() {
      const q = encodeAdres(addrInput.value);
      reqCode.textContent = 'GET /Api/Adres?adres=' + q;
      try {
        localStorage.setItem('woz_api_try_adres', addrInput.value);
      } catch (_) {}
    }

    function setSkeletonVisible(isVisible) {
      if (skeleton) skeleton.classList.toggle('d-none', !isVisible);
      if (responsePre) responsePre.classList.toggle('d-none', isVisible);
    }

    function revealResponse() {
      if (!responsePanel || responsePanel.classList.contains('is-visible')) return;
      responsePanel.classList.add('is-visible');
      responsePanel.setAttribute('aria-hidden', 'false');
    }

    function setBusy(isBusy) {
      if (responseCard) responseCard.setAttribute('aria-busy', isBusy ? 'true' : 'false');
      btnFetch.disabled = !!isBusy;
      setSkeletonVisible(!!isBusy);
    }

    function renderNotLoggedIn() {
      respCode.textContent = JSON.stringify(
        {
          fout: 'Niet ingelogd',
          tip: 'Log in om live aanvragen te doen. Voorbeelden en docs zijn altijd beschikbaar.',
          links: { inloggen: '/Identity/Account/Login', start: '/Identity/Account/Register' }
        },
        null,
        2
      );
    }

    function renderLimitReached(detail) {
      respCode.textContent = JSON.stringify(
        {
          fout: 'Gratis limiet bereikt',
          detail: detail || 'Je hebt je gratis test calls gebruikt. Log in om verder te gaan.',
          links: { inloggen: '/Identity/Account/Login', start: '/Identity/Account/Register' }
        },
        null,
        2
      );
    }

    async function doFetch() {
      const adres = addrInput.value.trim();
      revealResponse();

      if (!adres) {
        respCode.textContent = JSON.stringify(
          { fout: 'Vul een adres in, bijvoorbeeld: Spuistraat 36C, 1012 TT Amsterdam' },
          null,
          2
        );
        addrInput.focus();
        return;
      }

      setBusy(true);

      const url = '/Api/Adres?adres=' + encodeAdres(adres);

      try {
        const res = await fetch(url, { headers: { Accept: 'application/json' }, credentials: 'same-origin' });
        const ct = res.headers.get('content-type') || '';

        if (res.status === 401) {
          renderNotLoggedIn();
          return;
        }

        if (res.status === 402) {
          let body;
          try {
            body = await res.json();
          } catch (_) {
            body = { title: 'Onvoldoende credits' };
          }
          respCode.textContent = JSON.stringify({ fout: 'Onvoldoende credits', detail: body && body.detail ? body.detail : undefined }, null, 2);
          return;
        }

        if (res.status === 403) {
          let body;
          try {
            body = await res.json();
          } catch (_) {
            body = {};
          }
          renderLimitReached(body ? body.detail : undefined);
          return;
        }

        if (!res.ok) {
          if (ct.includes('application/json')) {
            try {
              respCode.textContent = JSON.stringify(await res.json(), null, 2);
              return;
            } catch (_) {}
          }
          let text = '';
          try {
            text = await res.text();
          } catch (_) {}
          respCode.textContent = JSON.stringify({ fout: 'Fout bij ophalen', status: res.status, detail: text || undefined }, null, 2);
          return;
        }

        let data;
        if (ct.includes('application/json')) {
          data = await res.json();
        } else {
          const raw = await res.text();
          try {
            data = JSON.parse(raw);
          } catch (_) {
            data = { raw: raw };
          }
        }

        respCode.textContent = JSON.stringify(data, null, 2);
      } catch (err) {
        respCode.textContent = JSON.stringify({ fout: 'Netwerkfout', detail: String(err) }, null, 2);
      } finally {
        setBusy(false);
      }
    }

    function showCopyToast(text) {
      if (!copyToast) return;
      copyToast.textContent = text;
      copyToast.classList.add('is-on');
      if (copyToastTimer) window.clearTimeout(copyToastTimer);
      copyToastTimer = window.setTimeout(() => {
        copyToast.classList.remove('is-on');
      }, 1200);
    }

    async function copyRequest() {
      if (!btnCopy) return;

      const full = window.location.origin + '/Api/Adres?adres=' + encodeAdres(addrInput.value);
      const originalText = btnCopy.textContent;

      try {
        if (navigator.clipboard && navigator.clipboard.writeText) {
          await navigator.clipboard.writeText(full);
        } else {
          await copyViaFallback(full);
        }
        btnCopy.classList.remove('is-copied');
        void btnCopy.offsetWidth;
        btnCopy.classList.add('is-copied');
        btnCopy.textContent = 'Gekopieerd';
        showCopyToast('URL gekopieerd');
      } catch (_) {
        btnCopy.textContent = 'Niet gelukt';
        showCopyToast('Kopieren is niet gelukt');
      } finally {
        window.setTimeout(() => {
          btnCopy.textContent = originalText;
          btnCopy.classList.remove('is-copied');
        }, 1200);
      }
    }

    try {
      const saved = localStorage.getItem('woz_api_try_adres');
      if (saved) addrInput.value = saved;
    } catch (_) {}

    updateRequestPreview();
    addrInput.addEventListener('input', updateRequestPreview);
    btnFetch.addEventListener('click', doFetch);
    if (btnCopy) btnCopy.addEventListener('click', copyRequest);
  }

  function initFaqAccordion(prefersReducedMotion) {
    const faq = document.getElementById('faqAcc');
    if (!faq || !faq.classList.contains('faq-accordion')) return;

    const items = Array.from(faq.querySelectorAll('.faq-item'));
    if (!items.length) return;

    function setItemOpen(item, isOpen) {
      const trigger = item.querySelector('.faq-trigger');
      const panel = item.querySelector('.faq-panel');
      if (!trigger || !panel) return;

      item.classList.toggle('is-open', isOpen);
      trigger.classList.toggle('collapsed', !isOpen);
      trigger.setAttribute('aria-expanded', isOpen ? 'true' : 'false');

      if (isOpen) {
        panel.style.maxHeight = panel.scrollHeight + 'px';
      } else {
        panel.style.maxHeight = '0px';
      }

      if (prefersReducedMotion) {
        panel.style.maxHeight = isOpen ? 'none' : '0px';
      }
    }

    const initiallyOpen = items.find((item) => item.classList.contains('is-open')) || items[0];

    items.forEach((item) => {
      const trigger = item.querySelector('.faq-trigger');
      const panel = item.querySelector('.faq-panel');
      if (!trigger || !panel) return;

      const controls = trigger.getAttribute('aria-controls');
      if (!controls || controls !== panel.id) {
        if (!panel.id) panel.id = 'faq-panel-' + Math.random().toString(36).slice(2, 10);
        trigger.setAttribute('aria-controls', panel.id);
      }

      setItemOpen(item, item === initiallyOpen);

      trigger.addEventListener('click', () => {
        const isOpen = item.classList.contains('is-open');
        if (isOpen) {
          setItemOpen(item, false);
          return;
        }

        items.forEach((other) => setItemOpen(other, other === item));
      });
    });

    window.addEventListener('resize', () => {
      items.forEach((item) => {
        if (!item.classList.contains('is-open')) return;
        const panel = item.querySelector('.faq-panel');
        if (!panel) return;
        panel.style.maxHeight = prefersReducedMotion ? 'none' : panel.scrollHeight + 'px';
      });
    });
  }

  onReady(function () {
    const prefersReducedMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    initReveal(prefersReducedMotion);
    initSurfaceGlow(prefersReducedMotion);
    initTryDemo();
    initFaqAccordion(prefersReducedMotion);
  });
})();
