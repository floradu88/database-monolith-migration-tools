import { Pipe, PipeTransform } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

/** Lightweight markdown → HTML for Codegraph chat replies (no external deps). */
@Pipe({ name: 'chatMarkdown', standalone: true })
export class ChatMarkdownPipe implements PipeTransform {
  constructor(private readonly sanitizer: DomSanitizer) {}

  transform(value: string | null | undefined): SafeHtml {
    if (!value) {
      return '';
    }

    const escaped = value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;');

    let html = escaped;
    html = html.replace(/```(\w*)\n?([\s\S]*?)```/g, (_m, _lang, code) =>
      `<pre class="md-code"><code>${code.trimEnd()}</code></pre>`);
    html = html.replace(/`([^`\n]+)`/g, '<code class="md-inline">$1</code>');
    html = html.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
    html = html.replace(/^### (.+)$/gm, '<h3>$1</h3>');
    html = html.replace(/^---$/gm, '<hr />');
    html = html.replace(/^- (.+)$/gm, '<li>$1</li>');
    html = html.replace(/(?:<li>[\s\S]*?<\/li>\n?)+/g, (block) => `<ul>${block}</ul>`);
    html = html.replace(/\n{2,}/g, '</p><p>');
    html = html.replace(/\n/g, '<br />');
    html = `<p>${html}</p>`;
    html = html.replace(/<p>\s*<(ul|pre|h3|hr)/g, '<$1');
    html = html.replace(/<\/(ul|pre|h3)>\s*<\/p>/g, '</$1>');
    html = html.replace(/<p>\s*<\/p>/g, '');

    return this.sanitizer.bypassSecurityTrustHtml(html);
  }
}
