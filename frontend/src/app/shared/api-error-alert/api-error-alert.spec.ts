import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ApiErrorAlert } from './api-error-alert';

// Uyarı kutusunu verilen hatayla oluşturur ve ekranda görünen metni döner
async function render(error: HttpErrorResponse, action?: string): Promise<string> {
  const fixture = TestBed.createComponent(ApiErrorAlert);
  fixture.componentRef.setInput('error', error);
  if (action) {
    fixture.componentRef.setInput('action', action);
  }
  await fixture.whenStable();
  return (fixture.nativeElement as HTMLElement).textContent!.replace(/\s+/g, ' ').trim();
}

// API'nin döndüğü hata gövdesi (ProblemDetails)
const problem = (status: number) => ({ title: 'x', status, traceId: '00-abc123-01' });

describe('ApiErrorAlert', () => {
  it('says the API is unreachable when there is no response (status 0)', async () => {
    const text = await render(new HttpErrorResponse({ status: 0 }));
    expect(text).toContain("API'ye ulaşılamıyor");
    expect(text).toContain('dotnet run');
    expect(text).not.toContain('Hata kodu');
  });

  it('says it is a server error for 500 and shows the trace id', async () => {
    const text = await render(new HttpErrorResponse({ status: 500, error: problem(500) }));
    expect(text).toContain('Sunucuda bir hata oluştu.');
    expect(text).not.toContain('ulaşılamıyor');
    expect(text).toContain('Hata kodu: 00-abc123-01');
  });

  it('names the failed action for 404', async () => {
    const text = await render(
      new HttpErrorResponse({ status: 404, error: problem(404) }),
      'Alarm okundu işaretlenemedi',
    );
    expect(text).toContain('Alarm okundu işaretlenemedi: İstenen kayıt bulunamadı.');
  });

  it('says the request is invalid for 400', async () => {
    const text = await render(new HttpErrorResponse({ status: 400, error: problem(400) }));
    expect(text).toContain('İstek geçersiz.');
  });

  it('shows no trace id when the body is not ProblemDetails', async () => {
    const text = await render(new HttpErrorResponse({ status: 502, error: 'Bad Gateway' }));
    expect(text).toContain('Sunucuda bir hata oluştu.');
    expect(text).not.toContain('Hata kodu');
  });
});
