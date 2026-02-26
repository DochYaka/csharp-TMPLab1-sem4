namespace Library.Exceptions
{
    public class FirstComponentInListException(string message = "Компонент первый в коллекции!") : Exception(message);
}