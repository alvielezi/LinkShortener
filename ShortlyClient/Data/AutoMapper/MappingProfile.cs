using AutoMapper;
using ShortlyData.Models;
using ShortlyClient.Data.ViewModels;

namespace ShortlyClient.Data.AutoMapper
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // AppUser -> GetUserVM
            CreateMap<AppUser, GetUserVM>()
                .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id))
                .ForMember(d => d.FullName, opt => opt.MapFrom(s => s.FullName));

            // Url -> GetUrlVM (maps nested User using the AppUser -> GetUserVM mapping)
            CreateMap<Url, GetUrlVM>()
                .ForMember(d => d.User, opt => opt.MapFrom(s => s.User));
        }
    }
}