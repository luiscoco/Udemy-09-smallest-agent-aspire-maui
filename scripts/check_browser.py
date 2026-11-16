"""Optional QA helper; Python/Playwright are not application dependencies."""
import json
from pathlib import Path
import httpx
from playwright.sync_api import sync_playwright, expect

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'verification'
OUT.mkdir(exist_ok=True)
API = 'http://127.0.0.1:8400'
WEB = 'http://127.0.0.1:5000'
health = httpx.get(API + '/api/health', timeout=10).json()
if health.get('api_key_configured'):
    raise RuntimeError('No-credit verification expects an unconfigured API; no prompt was sent.')
assert health['status'] == 'ok'
with httpx.Client(base_url=API, timeout=10, follow_redirects=True) as client:
    assert client.get('/docs').status_code == 200
    assert '/api/agent/stream' in client.get('/openapi.json').json()['paths']
    assert client.get('/health').status_code == 200
    assert client.get('/api/unknown').status_code == 404
    for mode in ['query','stream']:
        path = '/api/agent/' + mode
        assert client.post(path,json={'prompt':''}).status_code == 422
        assert client.post(path,json={'prompt':'Hi'},headers={'Origin':'https://foreign.invalid'}).status_code == 403
        assert client.post(path,content='{broken',headers={'Content-Type':'application/json'}).status_code == 400
        assert client.post(path,content='{}',headers={'Content-Type':'text/plain'}).status_code == 415
        assert client.post(path,json={'prompt':'x'*40000}).status_code == 413
    assert client.post('/api/agent/query',json={'prompt':'Hi'}).status_code == 503
    stream=client.post('/api/agent/stream',json={'prompt':'Hi'})
    assert 'event: error' in stream.text and 'event: done' not in stream.text
with sync_playwright() as playwright:
    browser=playwright.chromium.launch(executable_path='C:/Program Files/Google/Chrome/Application/chrome.exe',headless=True)
    page=browser.new_page(viewport={'width':1440,'height':1100})
    errors=[]
    page.on('pageerror',lambda error:errors.append(str(error)))
    page.goto(WEB)
    expect(page.locator('.run-button')).to_be_enabled(timeout=30000)
    expect(page.get_by_text('Server online',exact=True)).to_be_visible()
    page.locator('#prompt').fill('Remember the Aspire lesson')
    page.screenshot(path=str(OUT/'desktop.png'))
    for path,heading in [('/learn','Small enough to understand. Real enough to explore.'),('/setup','One AppHost. Shared native and web UI.'),('/architecture','See how the pieces connect.'),('/walkthrough','The same lesson. Native C#.'),('/','Start small. See everything.')]:
        page.locator('.lesson-nav a[href="'+path+'"]').click()
        expect(page.get_by_role('heading',name=heading,exact=True)).to_be_visible()
        expect(page.locator('.lesson-nav a[aria-current=page]')).to_have_attribute('href',path)
        if path=='/architecture':
            expect(page.locator('.workflow-code-step')).to_have_count(16)
            expect(page.locator('.hljs-keyword').first).to_be_attached()
            page.locator('.uml-card').scroll_into_view_if_needed()
            page.screenshot(path=str(OUT/'workflow.png'))
            with page.expect_download() as download:
                page.get_by_role('link',name='Download UML diagram (SVG)',exact=True).click()
            assert download.value.suggested_filename == 'workflow.svg'
    expect(page.locator('#prompt')).to_have_value('Remember the Aspire lesson')
    page.get_by_role('button',name='Run agent ↗',exact=True).click()
    expect(page.get_by_role('alert')).to_contain_text('Configure ANTHROPIC_API_KEY',timeout=15000)
    expect(page.locator('.run-button')).to_be_enabled()
    page.get_by_role('button',name='Simple',exact=True).click()
    page.locator('#prompt').focus()
    page.keyboard.press('Control+Enter')
    expect(page.get_by_role('alert')).to_contain_text('Configure ANTHROPIC_API_KEY')
    page.get_by_role('button',name='Toggle light or dark theme').click()
    assert page.locator('html').get_attribute('class') == 'dark'
    page.reload()
    expect(page.locator('html')).to_have_class('dark',timeout=15000)
    page.get_by_role('button',name='Toggle light or dark theme').click()
    page.set_viewport_size({'width':430,'height':930})
    page.screenshot(path=str(OUT/'mobile.png'))
    assert page.evaluate('document.documentElement.scrollWidth <= window.innerWidth'), 'Horizontal overflow'
    assert not errors, errors
    browser.close()
print('Real HTTP and Chrome checks passed: API contracts, shared UI tab clicks, retained prompt, UML download, both modes, Ctrl+Enter, theme and responsive layout.')
