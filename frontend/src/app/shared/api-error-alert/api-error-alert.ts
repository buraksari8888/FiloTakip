import { Component } from '@angular/core';

// "API'ye ulaşılamıyor" uyarı kutusu. Araç listesi, alarm listesi ve harita sayfası bunu kullanır.
@Component({
  selector: 'app-api-error-alert',
  styleUrl: './api-error-alert.scss',
  templateUrl: './api-error-alert.html',
})
export class ApiErrorAlert {}
