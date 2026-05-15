using AutoMapper;
using ShortlyClient.Data.ViewModels;
using ShortlyData.Models;

namespace ShortlyClient.Data.Mapper
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<Url, GetUrlVM>().ReverseMap();
           // CreateMap<AppUser, GetUserVM>().ReverseMap();
        }
    }
}