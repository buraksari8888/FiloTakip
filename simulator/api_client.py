"""FiloTakip API'si ile konuşan istemci. Bütün HTTP istekleri bu dosyada.

Bu dosya ekrana bir şey yazmaz; sonucu döner ya da hata fırlatır, ne yapılacağına çağıran karar verir.
"""

import requests

TIMEOUT_SECONDS = 5


class ApiError(Exception):
    """API bir hata kodu döndü (4xx/5xx). API hataları ProblemDetails biçiminde gelir:
    {"title": ..., "status": ..., "traceId": ..., "errors": {...}}
    """

    def __init__(self, response):
        try:
            problem = response.json()
        except ValueError:   # gövde JSON değil (ör. araya giren başka bir sunucunun HTML hata sayfası)
            problem = None
        if not isinstance(problem, dict):
            problem = {}

        self.status = response.status_code
        self.title = problem.get("title") or response.reason   # reason: "Bad Gateway" gibi standart açıklama
        self.errors = problem.get("errors")                    # doğrulama hataları (400): {"Alan": ["mesaj"]}
        self.trace_id = problem.get("traceId")                 # bu numarayla hata API'nin logunda bulunur

        # Doğrulama hatası varsa genel başlık yerine hangi alanın neden reddedildiğini yaz
        message = f"{self.status} {self.errors or self.title}"
        if self.trace_id:
            message += f" (traceId {self.trace_id})"
        super().__init__(message)


class FiloTakipApi:
    """API'ye istek atar. Adres dışarıdan verilir, böylece başka bir adresteki API'ye de bağlanabilir."""

    def __init__(self, base_url):
        self.base_url = base_url.rstrip("/")   # sondaki "/" adreste çift eğik çizgi oluşturmasın
        self.session = requests.Session()      # aynı bağlantıyı tekrar kullanır, her istekte yeniden bağlanmaz

    def get_vehicles(self):
        """Araç listesini çeker."""
        response = self.session.get(f"{self.base_url}/api/vehicles", timeout=TIMEOUT_SECONDS)
        self._check(response)
        return response.json()       # JSON metnini Python listesine çevir

    def get_vehicle(self, vehicle_id):
        """Tek bir aracı çeker, yoksa None döner."""
        response = self.session.get(f"{self.base_url}/api/vehicles/{vehicle_id}", timeout=TIMEOUT_SECONDS)
        if response.status_code == 404:
            return None
        self._check(response)
        return response.json()

    def send_location(self, payload):
        """Konumu gönderir, oluşan kaydın numarasını döner."""
        response = self.session.post(f"{self.base_url}/api/locations", json=payload, timeout=TIMEOUT_SECONDS)
        self._check(response)
        return response.json()["locationId"]

    @staticmethod
    def _check(response):
        """Hata kodu geldiyse (4xx/5xx) ApiError fırlatır."""
        if not response.ok:
            raise ApiError(response)
