    # Debug Board

Bu proje, teknisyen atamaları ve planlamalarını görselleştirmek için geliştirilmiş bir web uygulamasıdır.

## Özellikler

- Teknisyen çalışma saatlerinin görselleştirilmesi
- Atanan işlerin timeline üzerinde gösterimi
- Optimal ve sub-optimal atamaların farklı renklerle belirtilmesi
- Atanmamış işlerin ayrı bir bölümde listelenmesi
- Non-availability zamanlarının gösterimi
- Farklı zaman dilimlerine göre görüntüleme
- Detaylı tooltip bilgileri

## Dosya Yapısı

```
debug-board/
├── debug_board.html
├── request.json
├── response.json
├── styles/
│   └── main.css
└── scripts/
    ├── constants.js
    ├── helpers.js
    ├── renderers.js
    └── main.js
```

## Kullanım

1. JSON dosyalarını yükleyin
2. İstediğiniz zaman dilimini seçin
3. Timeline üzerinde:
   - Beyaz alanlar: Teknisyenin çalışma saatleri
   - Mavi kutular: Normal atamalar
   - Yeşil kutular: Optimal atamalar
   - Kırmızı alanlar: Non-availability zamanları
4. Sağ tarafta atanmamış işlerin listesi görüntülenir
5. Detaylı bilgi için kutuların üzerine gelin

## Gereksinimler

- Modern bir web tarayıcısı (Chrome, Firefox, Safari, Edge)
- JSON formatında request ve response verileri

## Geliştirme

Projeyi yerel ortamda çalıştırmak için:

1. Repo'yu klonlayın
2. `debug_board.html` dosyasını bir web sunucusu üzerinden açın
3. Test için örnek JSON dosyalarını kullanın

## Not

Bu uygulama, atama optimizasyonu sonuçlarını görselleştirmek ve debug etmek için tasarlanmıştır.
