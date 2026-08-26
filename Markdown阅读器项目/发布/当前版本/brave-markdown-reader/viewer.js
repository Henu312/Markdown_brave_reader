const content = document.querySelector('#content');
const emptyState = document.querySelector('#empty-state');
const fileName = document.querySelector('#file-name');
const fileInput = document.querySelector('#file-input');
const dropZone = document.querySelector('#drop-zone');
const themeToggle = document.querySelector('#theme-toggle');
const editToggle = document.querySelector('#edit-toggle');
const previewToggle = document.querySelector('#preview-toggle');
const saveFile = document.querySelector('#save-file');
const editorLayout = document.querySelector('#editor-layout');
const editor = document.querySelector('#editor');
const previewPane = document.querySelector('#preview-pane');
const editorPreview = document.querySelector('#editor-preview');
const tocPanel = document.querySelector('#toc-panel');
const tocLinks = document.querySelector('#toc-links');
let documentBaseUrl = '';
let documentAssetBaseUrl = '';
let currentDocument = { name: '未命名.md', text: '' };
let editing = false;
let previewVisible = localStorage.getItem('md-preview-visible') !== 'false';

function escapeHtml(value) {
  return value.replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[char]);
}

function safeUrl(value) {
  const url = value.trim();
  const embeddedImage = /^data:image\/(png|jpe?g|gif|webp);base64,[a-z0-9+/=\s]+$/i;
  if (/^(https?:|mailto:)/i.test(url) || embeddedImage.test(url)) return url;
  if (documentAssetBaseUrl && !/^[a-z][a-z0-9+.-]*:/i.test(url)) {
    return `${documentAssetBaseUrl}?path=${encodeURIComponent(url)}`;
  }
  if (documentBaseUrl && !/^[a-z][a-z0-9+.-]*:/i.test(url)) {
    try { return new URL(url, documentBaseUrl).href; } catch { return ''; }
  }
  return '';
}

function inline(source) {
  let value = escapeHtml(source);
  value = value.replace(/`([^`]+)`/g, '<code>$1</code>');
  value = value.replace(/!\[([^\]]*)\]\(([^ )]+)(?:\s+&quot;[^&]*&quot;)?\)/g, (_, alt, url) => {
    const safe = safeUrl(url); return safe ? `<img src="${safe}" alt="${alt}">` : alt;
  });
  value = value.replace(/\[([^\]]+)\]\(([^ )]+)\)/g, (_, label, url) => {
    const safe = safeUrl(url); return safe ? `<a href="${safe}" target="_blank" rel="noopener noreferrer">${label}</a>` : label;
  });
  value = value.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
  value = value.replace(/~~([^~]+)~~/g, '<del>$1</del>');
  value = value.replace(/(^|[^*])\*([^*]+)\*/g, '$1<em>$2</em>');
  return value;
}

function render(markdown) {
  const lines = markdown.replace(/\r\n?/g, '\n').split('\n');
  const html = [];
  let paragraph = [];
  let list = null;
  let inCode = false;
  let code = [];
  let headingNumber = 0;

  const flushParagraph = () => {
    if (paragraph.length) html.push(`<p>${inline(paragraph.join(' '))}</p>`);
    paragraph = [];
  };
  const closeList = () => {
    if (list) html.push(`</${list}>`);
    list = null;
  };

  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index];
    if (/^```/.test(line)) {
      flushParagraph(); closeList();
      if (inCode) html.push(`<pre><code>${escapeHtml(code.join('\n'))}</code></pre>`);
      inCode = !inCode; code = [];
      continue;
    }
    if (inCode) { code.push(line); continue; }
    if (!line.trim()) { flushParagraph(); closeList(); continue; }
    const heading = line.match(/^(#{1,6})\s+(.+)$/);
    if (heading) {
      flushParagraph(); closeList();
      const level = heading[1].length;
      html.push(`<h${level} id="heading-${headingNumber}">${inline(heading[2])}</h${level}>`);
      headingNumber += 1;
      continue;
    }
    if (/^\s{0,3}([-*_])(?:\s*\1){2,}\s*$/.test(line)) { flushParagraph(); closeList(); html.push('<hr>'); continue; }
    const quote = line.match(/^>\s?(.*)$/);
    if (quote) { flushParagraph(); closeList(); html.push(`<blockquote>${inline(quote[1])}</blockquote>`); continue; }
    const item = line.match(/^\s*([-+*]|\d+\.)\s+(.+)$/);
    if (item) {
      flushParagraph();
      const kind = /\d+\./.test(item[1]) ? 'ol' : 'ul';
      if (list !== kind) { closeList(); html.push(`<${kind}>`); list = kind; }
      const task = item[2].match(/^\[([ xX])\]\s+(.*)$/);
      html.push(task ? `<li class="task"><input type="checkbox" disabled ${task[1] !== ' ' ? 'checked' : ''}> ${inline(task[2])}</li>` : `<li>${inline(item[2])}</li>`);
      continue;
    }
    const tableDivider = /^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$/.test(lines[index + 1] || '');
    if (line.includes('|') && tableDivider) {
      flushParagraph(); closeList();
      const headers = line.replace(/^\||\|$/g, '').split('|').map(cell => `<th>${inline(cell.trim())}</th>`).join('');
      html.push(`<table><thead><tr>${headers}</tr></thead><tbody>`); index += 2;
      while (index < lines.length && lines[index].includes('|') && lines[index].trim()) {
        const cells = lines[index].replace(/^\||\|$/g, '').split('|').map(cell => `<td>${inline(cell.trim())}</td>`).join('');
        html.push(`<tr>${cells}</tr>`); index += 1;
      }
      html.push('</tbody></table>'); index -= 1; continue;
    }
    closeList(); paragraph.push(line.trim());
  }
  if (inCode) html.push(`<pre><code>${escapeHtml(code.join('\n'))}</code></pre>`);
  flushParagraph(); closeList();
  return html.join('\n');
}

function renderToc(markdown) {
  const entries = Array.from(markdown.replace(/\r\n?/g, '\n').matchAll(/^(#{1,6})\s+(.+)$/gm), (match, index) => ({
    level: match[1].length,
    title: match[2].replace(/`([^`]+)`/g, '$1').trim(),
    id: `heading-${index}`
  }));
  if (!entries.length) {
    tocPanel.hidden = true;
    tocLinks.innerHTML = '';
    return;
  }
  const baseLevel = Math.min(...entries.map(entry => entry.level));
  tocLinks.innerHTML = entries.map(entry => `<a href="#${entry.id}" data-target="${entry.id}" style="--toc-indent:${Math.min(entry.level - baseLevel, 4)}">${escapeHtml(entry.title)}</a>`).join('');
  tocPanel.hidden = false;
}

function jumpToHeading(event) {
  const link = event.target instanceof Element ? event.target.closest('a[data-target]') : null;
  if (!link || !tocLinks.contains(link)) return;
  event.preventDefault();
  const target = document.getElementById(link.dataset.target);
  if (!target) return;
  // 直接计算页面滚动位置，避免扩展页中 scrollIntoView 选错滚动容器。
  const scrollingElement = document.scrollingElement || document.documentElement;
  const targetTop = target.getBoundingClientRect().top + scrollingElement.scrollTop - 76;
  scrollingElement.scrollTo({ top: Math.max(0, targetTop), behavior: 'smooth' });
  try { history.replaceState(null, '', `#${encodeURIComponent(link.dataset.target)}`); } catch { /* 某些受限页面不允许修改地址栏，跳转本身不受影响。 */ }
}

async function showDocument(markdownDocument) {
  currentDocument = markdownDocument;
  editing = false;
  editorLayout.hidden = true;
  previewToggle.hidden = true;
  previewVisible = localStorage.getItem('md-preview-visible') !== 'false';
  applyPreviewVisibility();
  saveFile.hidden = true;
  editToggle.textContent = '\u270e';
  editToggle.title = '编辑 Markdown';
  editToggle.setAttribute('aria-label', editToggle.title);
  fileName.textContent = markdownDocument.name;
  window.document.title = `${markdownDocument.name} - Markdown 阅读器`;
  content.innerHTML = render(markdownDocument.text);
  editor.value = markdownDocument.text;
  editorPreview.innerHTML = content.innerHTML;
  renderToc(markdownDocument.text);
  content.hidden = false;
  emptyState.hidden = true;
  await chrome.storage.local.set({ activeDocument: markdownDocument });
}

function setEditMode(enabled) {
  if (!enabled) {
    currentDocument.text = editor.value;
    content.innerHTML = render(currentDocument.text);
    renderToc(currentDocument.text);
  }
  editing = enabled;
  editToggle.textContent = enabled ? '\u2716' : '\u270e';
  editToggle.title = enabled ? '退出编辑' : '编辑 Markdown';
  editToggle.setAttribute('aria-label', editToggle.title);
  saveFile.hidden = !enabled;
  previewToggle.hidden = !enabled;
  editorLayout.hidden = !enabled;
  content.hidden = enabled || !currentDocument.text;
  tocPanel.hidden = enabled || !currentDocument.text.match(/^(#{1,6})\s+.+$/m);
  if (enabled) {
    applyPreviewVisibility();
    editor.focus();
    editorPreview.scrollTop = 0;
  }
}

function applyPreviewVisibility() {
  editorLayout.classList.toggle('preview-hidden', !previewVisible);
  previewPane.hidden = !previewVisible;
  previewToggle.textContent = '\u{1F441}';
  previewToggle.title = previewVisible ? '隐藏预览' : '显示预览';
  previewToggle.setAttribute('aria-label', previewToggle.title);
}

function updatePreview() {
  currentDocument.text = editor.value;
  editorPreview.innerHTML = render(editor.value);
  renderToc(editor.value);
}

function saveCopy() {
  const blob = new Blob([editor.value], { type: 'text/markdown;charset=utf-8' });
  const link = document.createElement('a');
  link.href = URL.createObjectURL(blob);
  link.download = currentDocument.name.toLowerCase().endsWith('.md') ? currentDocument.name : `${currentDocument.name}.md`;
  link.click();
  URL.revokeObjectURL(link.href);
}

async function openFile(file) {
  if (!file) return;
  documentBaseUrl = '';
  documentAssetBaseUrl = '';
  await showDocument({ name: file.name, text: await file.text(), updatedAt: Date.now() });
}

async function openExternalFile(fileUrl) {
  if (!/^file:\/\//i.test(fileUrl)) throw new Error('仅允许打开本地 file:// 文件。');
  const response = await fetch(fileUrl);
  if (!response.ok) throw new Error(`读取失败（HTTP ${response.status}）。`);
  const name = decodeURIComponent(new URL(fileUrl).pathname.split('/').pop() || 'Markdown 文件');
  documentBaseUrl = fileUrl;
  await showDocument({ name, text: await response.text(), updatedAt: Date.now() });
}

async function openExternalSource(sourceUrl, assetBaseUrl) {
  if (!/^https?:\/\/127\.0\.0\.1(?::\d+)?\//i.test(sourceUrl)) throw new Error('仅允许读取本机预览服务。');
  const response = await fetch(sourceUrl);
  if (!response.ok) throw new Error(`读取失败（HTTP ${response.status}）。`);
  documentBaseUrl = sourceUrl;
  documentAssetBaseUrl = assetBaseUrl || '';
  const name = new URLSearchParams(window.location.search).get('name') || 'Markdown 文件';
  await showDocument({ name, text: await response.text(), updatedAt: Date.now() });
}

fileInput.addEventListener('change', () => openFile(fileInput.files[0]));
editToggle.addEventListener('click', () => setEditMode(!editing));
previewToggle.addEventListener('click', () => {
  previewVisible = !previewVisible;
  localStorage.setItem('md-preview-visible', String(previewVisible));
  applyPreviewVisibility();
});
saveFile.addEventListener('click', saveCopy);
editor.addEventListener('input', updatePreview);
tocLinks.addEventListener('click', jumpToHeading);
document.addEventListener('keydown', event => {
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's' && editing) {
    event.preventDefault();
    saveCopy();
  }
});
dropZone.addEventListener('dragover', event => { event.preventDefault(); dropZone.classList.add('dragging'); });
dropZone.addEventListener('dragleave', () => dropZone.classList.remove('dragging'));
dropZone.addEventListener('drop', event => { event.preventDefault(); dropZone.classList.remove('dragging'); openFile(event.dataTransfer.files[0]); });
themeToggle.addEventListener('click', () => { document.body.classList.toggle('dark'); chrome.storage.local.set({ dark: document.body.classList.contains('dark') }); });

(async () => {
  const params = new URLSearchParams(window.location.search);
  const externalSource = params.get('source');
  if (externalSource) {
    try { await openExternalSource(externalSource, params.get('assetBase') || ''); }
    catch (error) {
      emptyState.hidden = false;
      emptyState.querySelector('h1').textContent = '无法打开文件';
      emptyState.querySelector('p').textContent = error.message;
    }
    return;
  }
  const externalFile = params.get('file');
  if (externalFile) {
    try { await openExternalFile(externalFile); }
    catch (error) {
      emptyState.hidden = false;
      emptyState.querySelector('h1').textContent = '无法打开文件';
      emptyState.querySelector('p').textContent = error.message;
    }
    return;
  }
  const { activeDocument, dark } = await chrome.storage.local.get(['activeDocument', 'dark']);
  if (dark) document.body.classList.add('dark');
  if (activeDocument) showDocument(activeDocument);
})();
