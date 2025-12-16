# Dynamic Wheel Scanner for Unity

Unity için geliştirilmiş, fizik tabanlı gelişmiş bir tekerlek ve zemin tarama sistemi. Raycast ve Boxcast yöntemlerini hibrit kullanarak lastik genişliğini, batma miktarını (penetration) ve zemin eğimini hesaplar.

## Özellikler

* **Çoklu Dilim (Multi-Slice) Tarama:** Tekerleğin sadece merkezinden değil, genişliği boyunca (yanlardan) da tarama yaparak gerçekçi zemin teması sağlar.
* **Ağırlıklı Normal Hesaplama:** Zemin yüzeyinin normalini, lastiğin batma miktarına göre ağırlıklandırarak yumuşatır. Bu sayede engebeli arazilerde titreme yapmaz.
* **Görsel Hata Ayıklama (Gizmos):** Işınları, vuruş noktalarını ve hesaplanan normalleri Unity Editör penceresinde renkli olarak gösterir.
* **UPM Uyumlu:** Unity Package Manager ile direkt kurulabilir.

## Kurulum (Installation)

Bu paketi projenize eklemek için:

1.  Unity'de **Window > Package Manager** menüsünü açın.
2.  Sol üstteki **"+"** butonuna tıklayın.
3.  **"Add package from git URL..."** seçeneğini seçin.
4.  Aşağıdaki linki yapıştırın ve **Add** butonuna basın:

```text
[https://github.com/KULLANICI_ADIN/REPO_ADIN.git](https://github.com/KULLANICI_ADIN/REPO_ADIN.git)