

### Event management using BTCPay Server

If you are an event organizers, conference hosts, or community managers, you can now create and manage events on your Ghost platform that accept payments via Bitcoin using BTCPay Server. With this you can create an event, set ticket pricing and available quantities, allow attendees purchase their tickets using Bitcoin.

#### Prerequisites:

On BTCPay Server side, make sure you have set up [Emails for your store](https://docs.btcpayserver.org/Notifications/#store-emails). This is needed so that customers get all required information about the ticket purchase. 

#### Steps on how to set up events on Ghost via BTCPay Server.

1. Click on your BTCPay Server Ghost plugin on the left sidebar, below that menu entry you will see submenu entries, "Ghost Member" and "Ghost Event", click on "Ghost Event".

2. Click on "Create event" on the top right.

3. Fill out all required event information such as title, link (for an online event) or Address (for a physical event), Event logo, Description, Ticket fee and currency

	Event date, number of ticket for sale (if not an unlimited ticket event), the email subject and body that would be sent to customers on purchase of every ticket.

	N.B: It is important that you have configured your email service in server settings, this is needed so that customers get all required information needed in their email.

4. Once done, click on create. This should create the event successfully, and you should see it populated in the list of events available. The admin can delete the event, or edit details regarding the event.

5. From the table, you'd see the column: Ticket purchase link, which represent the link you'd put in your event page on Ghost. Copy the link, go to your event page

	edit the ticket purchase button and replace with the url that was copied

	
![BTCPay Server Ghost img 12](./img/Ghost/Ghost_Event_View.png)


![BTCPay Server Ghost img 13](./img/Ghost/Create_Ghost_Event_1.png)


![BTCPay Server Ghost img 14](./img/Ghost/Create_Ghost_Event_2.png)


![BTCPay Server Ghost img 15](./img/Ghost/Create_Ghost_Event_3.png)


![BTCPay Server Ghost img 16](./img/Ghost/Created_Ghost_Event_List_View.png)



6. When customers/potential attendees clicks on the link, they are redirected to a page to input their Name and Email and purchase ticket.

7. A BTCPay invoice is presented to the customer, to pay, and once paid, customer can then go ahead and download/print the invoice. 

8. If email is properly configured, the customer should get an email with details about the event as defined when creating an event. 



![BTCPay Server Ghost img 17](./img/Ghost/Purchase_Ticket_View.png)


![BTCPay Server Ghost img 18](./img/Ghost/Invoice_Generation.png)


![BTCPay Server Ghost img 19](./img/Ghost/Invoice_Payment.png)



9. Once customers starts purchasing tickets, the admin can view all the tickets being purchased. To view, Navigate to Ghost plugin >> Ghost event list  

10. Click on 'View Tickets', it would load all the tickets that have been purchased so far. The list includes the purchaser name, email, amount, invoiceId: which is a link to details regarding the particular purchase.

11. The admin can resend ticket information for any customer that didn't get an email after purchasing ticket. The "Resend Ticket Confirmation" button would trigger this



![BTCPay Server Ghost img 20](./img/Ghost/Paid_Event_List.png)


![BTCPay Server Ghost img 21](./img/Ghost/Paid_Event_Tickets.png)
