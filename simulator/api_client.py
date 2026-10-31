"""FiloTakip API'si ile konuşan istemci. Bütün HTTP istekleri bu dosyada.

Bu dosya ekrana bir şey yazmaz; sonucu döner ya da hata fırlatır, ne yapılacağına çağıran karar verir.
"""

import requests

TIMEOUT_SECONDS = 5


class LocationRejectedError(Exception):
    """API konumu geçersiz bulup reddetti (400 Bad Request)."""


class FiloTakipApi:
    """API'ye istek atar. Adres dışarıdan verilir, böylece başka bir adresteki API'ye de bağlanabilir."""

    def __init__(self, base_url):
        self.base_url = base_url.rstrip("/")   # sondaki "/" adreste çift eğik çizgi oluşturmasın
        self.session = requests.Session()      # aynı bağlantıyı tekrar kullanır, her istekte yeniden bağlanmaz

    def get_vehicles(self):
        """Araç listesini çeker."""
        response = self.session.get(f"{self.base_url}/api/vehicles", timeout=TIMEOUT_SECONDS)
        response.raise_for_status()  # 200 dışında bir kod gelirse hata fırlat
        return response.json()       # JSON metnini Python listesine çevir

    def get_vehicle(self, vehicle_id):
        """Tek bir aracı çeker, yoksa None döner."""
        response = self.session.get(f"{self.base_url}/api/vehicles/{vehicle_id}", timeout=TIMEOUT_SECONDS)
        if response.status_code == 404:
            return None
        response.raise_for_status()
        return response.json()

    def send_location(self, payload):
        """Konumu gönderir, oluşan kaydın numarasını döner."""
        response = self.session.post(f"{self.base_url}/api/locations", json=payload, timeout=TIMEOUT_SECONDS)
        if response.status_code == 400:
            raise LocationRejectedError(response.json().get("errors"))
        response.raise_for_status()
        return response.json()["locationId"]
