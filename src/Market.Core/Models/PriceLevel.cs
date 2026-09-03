using System.Runtime.InteropServices;

namespace Market.Core.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)] // Жесткое выравнивание в памяти (размер ровно 20 байт)
public readonly struct PriceLevel
{
    // 16 байт: Высокая точность для цен (крипта, дробные котировки)
    public readonly decimal Price;

    // 4 байта: Доступный объем ликвидности на данном уровне. 
    // Использование uint вместо decimal экономит 12 байт на каждый уровень стакана,
    // что критически важно при хранении 50+ уровней глубины в L1-кэше процессора.
    public readonly uint Volume;

    /// <summary>
    /// Конструктор для создания неизменяемого уровня цен.
    /// </summary>
    public PriceLevel(decimal price, uint volume)
    {
        Price = price;
        Volume = volume;
    }

    /// <summary>
    /// Проверка, пустой ли уровень (используется для предвыделенных массивов стакана)
    /// </summary>
    public bool IsEmpty => Volume == 0;
}
