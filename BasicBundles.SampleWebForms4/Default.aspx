<%@ Page Title="" Language="C#" MasterPageFile="~/Default.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="BasicBundles.SampleWebForms4.MyWebForm1" %>

<%@ Import Namespace="BasicBundles.SampleWebForms4" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <% BundleConfig.DefaultRequirements.Require(); %>
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <h2>Hello, this is a sample page.</h2>
</asp:Content>
