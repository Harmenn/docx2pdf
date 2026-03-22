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

(function () {
  try {
    const dropzone = document.getElementById('demo-dropzone');
    const fileInput = document.getElementById('heroDocxInput');
    const statusLine = document.getElementById('heroDropStatus');
    const metaLine = document.getElementById('heroDropMeta');

    if (!dropzone || !fileInput || !statusLine || !metaLine) {
      return;
    }

    let processing = false;

    function setDropState(state) {
      dropzone.classList.toggle('is-dragover', state === 'dragover');
      dropzone.classList.toggle('is-processing', state === 'processing');
      dropzone.classList.toggle('is-success', state === 'success');
    }

    function setMessage(status, meta) {
      statusLine.textContent = status;
      metaLine.textContent = meta;
    }

    function escapePdfText(value) {
      return String(value).replace(/\\/g, '\\\\').replace(/\(/g, '\\(').replace(/\)/g, '\\)');
    }

    function buildDemoPdf(fileName) {
      const lines = [
        'Docx2Pdf demo',
        `Bestand: ${fileName}`,
        'Deze homepage laat de gewenste UX zien:',
        'DOCX erin. PDF eruit. Klaar.'
      ];

      const content = [
        'BT',
        '/F1 24 Tf',
        '72 760 Td',
        `(${escapePdfText(lines[0])}) Tj`,
        '0 -34 Td',
        '/F1 16 Tf',
        `(${escapePdfText(lines[1])}) Tj`,
        '0 -28 Td',
        `(${escapePdfText(lines[2])}) Tj`,
        '0 -24 Td',
        `(${escapePdfText(lines[3])}) Tj`,
        'ET'
      ].join('\n');

      const objects = [
        '1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n',
        '2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n',
        '3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>\nendobj\n',
        '4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n',
        `5 0 obj\n<< /Length ${content.length} >>\nstream\n${content}\nendstream\nendobj\n`
      ];

      let pdf = '%PDF-1.4\n';
      const offsets = [0];
      objects.forEach((object) => {
        offsets.push(pdf.length);
        pdf += object;
      });
      const xrefStart = pdf.length;
      pdf += `xref\n0 ${objects.length + 1}\n`;
      pdf += '0000000000 65535 f \n';
      for (let i = 1; i < offsets.length; i += 1) {
        pdf += `${String(offsets[i]).padStart(10, '0')} 00000 n \n`;
      }
      pdf += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xrefStart}\n%%EOF`;
      return new Blob([pdf], { type: 'application/pdf' });
    }

    function triggerDownload(file) {
      const pdfName = file.name.replace(/\.docx$/i, '') || 'document';
      const blob = buildDemoPdf(file.name);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `${pdfName}.pdf`;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 1500);
    }

    function rejectFile() {
      setDropState('');
      setMessage('Alleen even een .docx erin gooien.', 'PDF, ZIP of iets anders is hier niet de grap.');
      processing = false;
    }

    function processFile(file) {
      if (!file || !/\.docx$/i.test(file.name)) {
        rejectFile();
        return;
      }

      processing = true;
      setDropState('processing');
      setMessage(`Netjes. ${file.name} ligt op de stapel.`, 'PDF bakken zonder LibreOffice-circus...');

      window.setTimeout(() => {
        setMessage('Bijna klaar.', 'Nog heel even geen enterprise-gedoe simuleren.');
      }, 650);

      window.setTimeout(() => {
        triggerDownload(file);
        setDropState('success');
        setMessage('Hop, daar is ie. Was dat nou zo moeilijk?', 'Looking at you LibreOffice, Aspose.NET en Syncfusion?');
        processing = false;
      }, 1500);
    }

    ['dragenter', 'dragover'].forEach((eventName) => {
      dropzone.addEventListener(eventName, (event) => {
        event.preventDefault();
        if (processing) {
          return;
        }
        setDropState('dragover');
        setMessage('Ja hoor, precies daar.', 'Laat maar vallen. Zo hoort software te voelen.');
      });
    });

    ['dragleave', 'dragend'].forEach((eventName) => {
      dropzone.addEventListener(eventName, () => {
        if (processing) {
          return;
        }
        setDropState('');
        setMessage('Klaar voor een DOCX. Niet moeilijk doen, gewoon droppen.', 'Na de drop laten we direct de gewenste UX zien: uploaden, omzetten, downloaden.');
      });
    });

    dropzone.addEventListener('drop', (event) => {
      event.preventDefault();
      if (processing) {
        return;
      }
      const [file] = event.dataTransfer?.files || [];
      processFile(file);
    });

    dropzone.addEventListener('keydown', (event) => {
      if (event.key !== 'Enter' && event.key !== ' ') {
        return;
      }
      event.preventDefault();
      fileInput.click();
    });

    fileInput.addEventListener('change', () => {
      if (processing) {
        return;
      }
      const [file] = fileInput.files || [];
      processFile(file);
      fileInput.value = '';
    });
  } catch {
  }
})();
