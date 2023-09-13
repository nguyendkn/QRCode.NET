// Khai báo không gian tên

namespace QRCode.Abstracts
{
    /// <summary>
    /// Mức độ sửa lỗi. Những mức này xác định mức độ chịu đựng về phần trăm mã mất đi trước khi không thể phục hồi mã.
    /// </summary>
    public enum EccLevel
    {
        /// <summary>
        /// Có thể mất đi tới 7% mã trước khi không thể phục hồi
        /// </summary>
        L,

        /// <summary>
        /// Có thể mất đi tới 15% mã trước khi không thể phục hồi
        /// </summary>
        M,

        /// <summary>
        /// Có thể mất đi tới 25% mã trước khi không thể phục hồi
        /// </summary>
        Q,

        /// <summary>
        /// Có thể mất đi tới 30% mã trước khi không thể phục hồi
        /// </summary>
        H
    }
}