export function initialize() { try { document.documentElement.classList.toggle('dark',localStorage.getItem('agent-theme') === 'dark'); } catch {} }
export function toggleTheme() { const dark=document.documentElement.classList.toggle('dark'); try { localStorage.setItem('agent-theme',dark?'dark':'light'); } catch {} }
export async function copy(text) { await navigator.clipboard.writeText(text); }
export function download(text,filename='claude-answer.md',mime='text/markdown') { const url=URL.createObjectURL(new Blob([text],{type:mime+';charset=utf-8'}));const link=document.createElement('a');link.href=url;link.download=filename;link.click();URL.revokeObjectURL(url); }
