using MediatR;
using MediFlow.BuildingBlocks.Common;

namespace MediFlow.BuildingBlocks.Application;

/// <summary>Durumu DEĞİŞTİREN bir istek.</summary>
/// <remarks>
/// MediatR'ın IRequest'i yerine kendi arayüzümüz: niyet tipten okunur ve
/// behavior'lar yalnızca bir tarafı hedefleyebilir (transaction behavior'ı
/// sadece komutlara uygulanacak - sorgular veri değiştirmiyor).
/// Dönüş tipi Result'a sabit: hiçbir handler hatayı exception ile bildiremez.
/// </remarks>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}

/// <summary>Sadece OKUYAN bir istek.</summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}
