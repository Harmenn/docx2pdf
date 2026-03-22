(function () {
  try {
    const sliderShell = document.querySelector('.credit-slider-shell');
    const slider = document.getElementById('creditVolume');
    const input = document.getElementById('creditVolumeInput');
    const presets = Array.from(document.querySelectorAll('[data-credit-preset]'));
    const totalOutput = document.getElementById('creditSliderTotal');
    const countOutput = document.getElementById('creditSliderCount');
    const unitOutput = document.getElementById('creditSliderUnit');
    const countMetaOutput = document.getElementById('creditSliderCountMeta');
    const unitMetaOutput = document.getElementById('creditSliderUnitMeta');
    const subtotalOutput = document.getElementById('creditSliderSubtotal');
    const subtotalMetaOutput = document.getElementById('creditSliderSubtotalMeta');
    const vatOutput = document.getElementById('creditSliderVat');
    const discountOutput = document.getElementById('creditSliderDiscount');
    const discountMetaOutput = document.getElementById('creditSliderDiscountMeta');
    const errorOutput = document.getElementById('creditVolumeError');
    const creditOverviewCta = document.getElementById('creditOverviewCta');
    const quotesTarget = sliderShell?.dataset.quotesTarget;
    const quotesElement = quotesTarget ? document.getElementById(quotesTarget) : null;

    if (!sliderShell || !slider || !input || !countOutput || !countMetaOutput || !subtotalOutput || !errorOutput) {
      return;
    }

    const minCredits = Number(sliderShell.dataset.minCredits || 100);
    const maxCredits = Number(sliderShell.dataset.maxCredits || 2000);
    const sliderMin = Number(slider.min || 0);
    const sliderMax = Number(slider.max || 100);
    const minPrice = Number(sliderShell.dataset.minPrice || 0.45);
    const maxPrice = Number(sliderShell.dataset.maxPrice || 0.20);
    const priceCurveExponent = Number(sliderShell.dataset.priceCurveExponent || 0.28);
    const vatRate = Number(sliderShell.dataset.vatRate || 0.21);
    const numberNl = new Intl.NumberFormat('nl-NL');
    const euroNl = new Intl.NumberFormat('nl-NL', { style: 'currency', currency: 'EUR' });
    const quoteOptions = quotesElement ? JSON.parse(quotesElement.textContent || '[]') : [];
    const quotesByCredits = new Map(quoteOptions.map((quote) => [Number(quote.credits), quote]));

    function clampCredits(value) {
      return Math.min(maxCredits, Math.max(minCredits, value));
    }

    function parseCredits(value) {
      const normalized = String(value || '').trim().replace(/\./g, '').replace(',', '.');
      return normalized ? Number(normalized) : NaN;
    }

    function getStepForCredits(value) {
      return value < 500 ? 50 : 100;
    }

    function creditsToSliderValue(credits) {
      const clamped = clampCredits(credits);
      if (clamped <= 250) {
        return ((clamped - minCredits) / (250 - minCredits)) * 25;
      }
      return 25 + (((clamped - 250) / (maxCredits - 250)) * 75);
    }

    function sliderValueToCredits(value) {
      const clamped = Math.min(sliderMax, Math.max(sliderMin, value));
      if (clamped <= 25) {
        return minCredits + ((clamped / 25) * (250 - minCredits));
      }
      return 250 + (((clamped - 25) / 75) * (maxCredits - 250));
    }

    function snapCredits(value) {
      const clamped = clampCredits(Math.round(value));
      const step = getStepForCredits(clamped);
      if (clamped < 500) {
        return Math.round(clamped / step) * step;
      }
      return Math.round((clamped - 500) / step) * step + 500;
    }

    function setValidation(message) {
      errorOutput.textContent = message || '';
      input.setAttribute('aria-invalid', message ? 'true' : 'false');
      input.classList.toggle('is-invalid', Boolean(message));
    }

    function syncPresetState(credits) {
      presets.forEach((preset) => {
        preset.classList.toggle('is-active', Number(preset.dataset.creditPreset) === credits);
      });
    }

    function updateFlowLinks(credits) {
      if (creditOverviewCta) {
        creditOverviewCta.href = `/credits/summary?credits=${encodeURIComponent(String(credits))}`;
      }
    }

    function getQuote(credits) {
      const quote = quotesByCredits.get(credits);
      if (quote) {
        return quote;
      }
      const ratio = (credits - minCredits) / (maxCredits - minCredits);
      const curveRatio = Math.pow(Math.max(0, ratio), priceCurveExponent);
      const unitPriceExVat = minPrice - ((minPrice - maxPrice) * curveRatio);
      const subtotalExVat = credits * unitPriceExVat;
      const vatAmount = subtotalExVat * vatRate;
      const totalInclVat = subtotalExVat + vatAmount;
      const discountPercent = Math.max(0, Math.round(((minPrice - unitPriceExVat) / minPrice) * 100));
      return { credits, sliderPosition: creditsToSliderValue(credits), unitPriceExVat, subtotalExVat, vatAmount, totalInclVat, discountPercent };
    }

    function updateCreditSlider(credits) {
      const quote = getQuote(credits);
      const progress = ((Number(quote.sliderPosition) - sliderMin) / Math.max(1, sliderMax - sliderMin)) * 100;
      slider.value = String(quote.sliderPosition);
      slider.style.setProperty('--slider-progress', `${progress}%`);
      input.value = numberNl.format(credits);
      countOutput.textContent = numberNl.format(credits);
      countMetaOutput.textContent = numberNl.format(credits);
      if (unitOutput) unitOutput.textContent = euroNl.format(quote.unitPriceExVat);
      if (unitMetaOutput) unitMetaOutput.textContent = euroNl.format(quote.unitPriceExVat);
      if (subtotalOutput) subtotalOutput.textContent = euroNl.format(quote.subtotalExVat);
      if (subtotalMetaOutput) subtotalMetaOutput.textContent = euroNl.format(quote.subtotalExVat);
      if (vatOutput) vatOutput.textContent = euroNl.format(quote.vatAmount);
      if (totalOutput) totalOutput.textContent = euroNl.format(quote.totalInclVat);
      if (discountOutput) discountOutput.textContent = String(quote.discountPercent);
      if (discountMetaOutput) discountMetaOutput.textContent = `${quote.discountPercent}%`;
      syncPresetState(credits);
      updateFlowLinks(credits);
    }

    let lastValidCredits = snapCredits(parseCredits(input.value) || sliderValueToCredits(Number(slider.value)));
    updateCreditSlider(lastValidCredits);

    slider.addEventListener('input', function () {
      setValidation('');
      lastValidCredits = snapCredits(sliderValueToCredits(Number(slider.value)));
      updateCreditSlider(lastValidCredits);
    });

    input.addEventListener('blur', function () {
      const parsed = parseCredits(input.value);
      if (!Number.isFinite(parsed) || !Number.isInteger(parsed)) {
        setValidation('Vul een heel aantal credits in.');
        input.value = numberNl.format(lastValidCredits);
        return;
      }
      if (parsed < minCredits || parsed > maxCredits) {
        setValidation(`Kies tussen ${numberNl.format(minCredits)} en ${numberNl.format(maxCredits)} credits.`);
        input.value = numberNl.format(lastValidCredits);
        return;
      }
      setValidation('');
      lastValidCredits = snapCredits(parsed);
      updateCreditSlider(lastValidCredits);
    });

    presets.forEach((preset) => {
      preset.addEventListener('click', function () {
        setValidation('');
        lastValidCredits = snapCredits(Number(preset.dataset.creditPreset));
        updateCreditSlider(lastValidCredits);
      });
    });
  } catch {
  }
})();
