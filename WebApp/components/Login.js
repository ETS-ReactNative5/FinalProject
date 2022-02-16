import React from 'react';
import { Button, Image, SafeAreaView, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import Toast from 'react-native-simple-toast';
import AsyncStorage from '@react-native-async-storage/async-storage';
import Loading from './misc/Loading';
import { Colours, Spacing } from '../styles';

import Logo from '../images/DWNLogo.png';


const Login = ({ navigation }) => {
    // debugger;
    const [isLoading, setIsLoading] = React.useState(false);
    const [email, setEmail] = React.useState("");
    const [password, setPassword] = React.useState("");

    const CheckLoginDetails = () => {
        if (email !== "" && password !== "") {

            setIsLoading(true);

            var loginUser = {
                email: email,
                password: password
            }

            fetch('https://dogwalknationapi.azurewebsites.net/user/login', {
                method: 'POST',
                headers: {
                    Accept: 'application/json',
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(
                    loginUser
                )
            })
                .then((response) => response.json())
                .then((json) => {
                    console.log(json)
                    if (json.status !== 400) {

                        if (json.success === true) {
                            Toast.show("Login successful!");
                            try {
                                const userString = JSON.stringify(json.user);
                                AsyncStorage.setItem('User', userString)
                                    .then(() => {
                                        setIsLoading(false);
                                        //Clear password everytime the user does anything for security
                                        setPassword("");
                                        navigation.navigate("Home");
                                    })
                            } catch (e) {
                                // saving error
                                console.log(e)
                                setIsLoading(false);
                                setPassword("");
                            }
                        } else {
                            //Response from API
                            Toast.show(json.message);
                            setIsLoading(false);
                            setPassword("");
                        }
                    } else {
                        Toast.show("Please enter a valid email address")
                        setIsLoading(false);
                        setPassword("");
                    }
                })
                .catch((error) => {
                    console.error(error);
                    setIsLoading(false);
                    setPassword("");
                })
        } else {
            Toast.show("Please enter a username and password!")
        }
    }

    return (
        isLoading ?
            <Loading />
            :
            <SafeAreaView>
                <ScrollView keyboardShouldPersistTaps="always" contentInsetAdjustmentBehavior="automatic">
                    <View style={styles.mainView} >
                        <View style={styles.logoSection}>
                            <Image style={styles.logo} source={Logo}/>
                        </View>
                        <View style={styles.inputSection}>
                            <Text style={styles.loginText}>Please log in with your email and password to continue</Text>
                            <TextInput
                                value={email}
                                style={styles.input}
                                onChangeText={setEmail}
                                placeholder="Email Address"
                            />
                            <TextInput
                                style={styles.input}
                                onChangeText={setPassword}
                                placeholder="Password"
                                secureTextEntry={true}
                            />
                            <View styles={styles.submit}>
                                <Text></Text>
                                <Button title="Submit" onPress={CheckLoginDetails} />
                            </View>
                        </View>

                    </View>
                </ScrollView>
            </SafeAreaView>
    );
};

const styles = StyleSheet.create({
    mainView: {
        flex: 1,
        height: Spacing.screen.height * 0.9,
        justifyContent: 'center',
        alignItems: "center",
        flexDirection: "column",
        // backgroundColor: Colours.primary.base,
    },
    logoSection: {
        flex: 0.4,
        width: "100%",
        padding: 70,
        marginTop: -20,
        marginBottom: -20,
    },
    logo: {
        width: "100%",
        height: undefined,
        aspectRatio: 1,
        resizeMode: "contain",
    },
    inputSection: {
        flex: 0.6,
        width: "100%",
        alignItems: "center",
    },
    loginText: {
        marginTop: 15,
        fontSize: 15,
        // color: Colours.neutral.grey1,
    },
    input: {
        width: '60%',
        marginTop: 20,
        borderStyle: "solid",
        borderColor: "black",
        // borderColor: Colours.neutral.grey3,
        borderWidth: 1.5,
        // color: Colours.neutral.grey1,
    },
    submit: {
        width: "100%",
        paddingTop: 25,
    },
});

export default Login;
